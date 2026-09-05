# Launcher Minecraft personnalisé

Launcher WPF (.NET / C#) pour un serveur Minecraft privé (8 joueurs max), basé sur
[CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) et
[CmlLib.Core.Installer.Forge](https://github.com/CmlLib/CmlLib.Core.Installer.Forge).

## Contexte serveur

- Minecraft **1.16.5**, Forge **36.2.34**
- 87 mods (Botania, Create, Quark, Minecolonies, Twilight Forest, Biomes O'Plenty, ...)
- **Java 8 obligatoire** (Forge 1.16.5 est incompatible avec Java 11+)
- Mods/config hébergés sur un VPS perso, accessibles en HTTP direct (voir manifeste ci-dessous)

## Ce que fait le launcher

1. Vérifie/installe Java 8 (build Temurin/Adoptium si absent) — **implémenté**
2. Installe Forge 1.16.5-36.2.34 via CmlLib.Core.Installer.Forge — **implémenté**
3. Synchronise `mods/` et `config/` depuis le VPS (par hash, pas à chaque lancement) — **implémenté**
4. Réutilise la session déjà connectée du launcher officiel (`launcher_accounts.json`) — **implémenté**
5. Lance le jeu avec le bon classpath Forge et la RAM configurée — **implémenté**

Pas d'implémentation OAuth Microsoft : le launcher lit la session déjà authentifiée sur la machine.
Pas de gestion multi-comptes : usage privé entre amis, un seul compte par machine.

## Structure du repo

```
├── MinecraftLauncherPerso.sln
├── src/
│   └── MinecraftLauncherPerso/
│       ├── MinecraftLauncherPerso.csproj   # net8.0-windows, WPF, CmlLib.Core + Installer.Forge
│       ├── App.xaml(.cs)                   # bootstrap de l'application WPF
│       ├── MainWindow.xaml(.cs)            # UI + orchestration Java → Forge → Sync → Auth → Lancement
│       ├── Models/
│       │   ├── LauncherSettings.cs         # préférences persistées (RAM, dossier de jeu, URL VPS)
│       │   ├── JavaVersionInfo.cs
│       │   └── JavaSetupProgress.cs
│       └── Services/
│           ├── Java/                       # détection + installation Java 8
│           │   ├── IJavaManager.cs
│           │   ├── JavaManager.cs
│           │   └── AdoptiumApiClient.cs
│           ├── Forge/                      # installation Forge (CmlLib.Core.Installer.Forge)
│           │   ├── IForgeManager.cs
│           │   └── ForgeManager.cs
│           ├── ModSync/                    # synchro mods/config depuis le VPS par manifeste+hash
│           │   ├── IModSyncService.cs
│           │   ├── ModSyncService.cs
│           │   └── ModManifest.cs
│           ├── Auth/                       # lecture session launcher officiel
│           │   ├── IAuthService.cs
│           │   └── MinecraftAuthService.cs
│           ├── Launch/                     # construction + démarrage du process Forge/Minecraft
│           │   ├── IGameLauncher.cs
│           │   └── GameLauncher.cs
│           └── Configuration/
│               └── SettingsManager.cs      # charge/sauvegarde settings.json
├── README.md
└── .gitignore
```

Chaque responsabilité (Java, Forge, sync mods, auth, lancement) est isolée dans son propre
service derrière une interface (`IJavaManager`, `IForgeManager`, `IModSyncService`,
`IAuthService`, `IGameLauncher`), injectées dans `MainWindow` qui orchestre l'enchaînement complet
au clic sur "Jouer".

## Logique de vérification/installation de Java 8

Fichier : `src/MinecraftLauncherPerso/Services/Java/JavaManager.cs`

Ordre de résolution dans `EnsureJava8Async` :

1. **Java 8 portable déjà installé par ce launcher** : recherche récursive d'un exécutable
   `java(.exe)` sous `%AppData%/MinecraftLauncherPerso/runtime/java8`, validé en exécutant
   `java -version` et en vérifiant que la version majeure vaut bien 8.
2. **Java 8 déjà présent sur la machine** : `JAVA_HOME`, `java` sur le `PATH`, puis les dossiers
   d'installation courants sous Windows (`Program Files\Java`, `...\Eclipse Adoptium`,
   `...\AdoptOpenJDK`).
3. **Téléchargement automatique** : si aucun Java 8 valide n'est trouvé, interrogation de l'API
   [Adoptium](https://api.adoptium.net) (`/v3/assets/latest/8/hotspot`) pour récupérer la dernière
   build Temurin 8 (JRE) correspondant à l'OS/architecture de la machine, téléchargement avec
   suivi de progression, puis extraction dans le dossier `runtime/java8` ci-dessus.

La détection de version parse la sortie de `java -version` (`version "1.8.0_392"` →
version majeure 8 ; `version "17.0.9"` → version majeure 17), ce qui permet de rejeter tout
Java déjà installé qui ne serait pas une version 8, même si un JDK plus récent est présent.

## Installation de Forge

Fichier : `src/MinecraftLauncherPerso/Services/Forge/ForgeManager.cs`

Utilise le package `CmlLib.Core.Installer.Forge` :
`ForgeInstaller.Install(minecraftVersion, forgeVersion, options)` installe/mappe le profil de
version composé (vanilla + Forge) et retourne son identifiant (ex. `1.16.5-forge-36.2.34`).
Ce mapping seul ne télécharge pas les fichiers de la version : `MinecraftLauncher.InstallAsync`
est appelé juste après pour installer réellement le jar, les libs et les assets vanilla dont
Forge dépend.

## Synchronisation mods/config (VPS)

Fichier : `src/MinecraftLauncherPerso/Services/ModSync/ModSyncService.cs`

Le launcher attend un manifeste JSON à `{ModsServerBaseUrl}/manifest.json` sur le VPS, à héberger
et régénérer vous-même à chaque mise à jour du pack :

```json
{
  "version": "2026-01-01",
  "files": [
    { "path": "mods/create-1.16.5-0.3.2g.jar", "sha256": "<sha256 hex>", "url": "mods/create-1.16.5-0.3.2g.jar" },
    { "path": "config/create/common.toml", "sha256": "<sha256 hex>", "url": "config/create/common.toml" }
  ]
}
```

- `path` : chemin relatif au dossier de jeu (`GameDirectory`) où écrire le fichier.
- `sha256` : hash du contenu, utilisé pour décider si le fichier doit être (re)téléchargé, et pour
  vérifier l'intégrité après téléchargement.
- `url` : chemin relatif à `ModsServerBaseUrl` pour le télécharger (identique à `path` si vos mods
  et votre config sont servis directement à cette URL).

À chaque lancement, le launcher télécharge ce manifeste puis le compare à un manifeste local mis
en cache (`{GameDirectory}/launcher-mods-manifest.json`, écrit par le launcher lui-même) : seuls
les fichiers absents ou dont le hash a changé sont retéléchargés (pas de re-hash de tout le disque
à chaque lancement). Les fichiers qui ne sont plus référencés par le manifeste distant sont
supprimés localement (mod retiré du pack).

## Authentification (session du launcher officiel)

Fichier : `src/MinecraftLauncherPerso/Services/Auth/MinecraftAuthService.cs`

Lit `%AppData%/.minecraft/launcher_accounts.json` : cherche le compte référencé par
`activeAccountLocalId`, et en extrait `accessToken` + `minecraftProfile.name`/`.id` (pseudo et UUID
réellement utilisés en jeu — le champ racine `username` du compte est l'identifiant Microsoft/email,
pas le pseudo Minecraft). Lève une erreur explicite si le fichier est absent, si aucun compte actif
n'est trouvé, ou si le token a une date d'expiration dépassée — dans ces cas, l'utilisateur doit
rouvrir le launcher officiel et se reconnecter avant de relancer ce launcher.

## Lancement du jeu

Fichier : `src/MinecraftLauncherPerso/Services/Launch/GameLauncher.cs`

Construit une `MSession` (CmlLib.Core.Auth) à partir de la session lue ci-dessus, un
`MLaunchOption` avec `JavaPath`, `MinimumRamMb`/`MaximumRamMb`, puis appelle
`MinecraftLauncher.BuildProcessAsync(versionId, options)` et démarre le process obtenu. Le
launcher ne bloque pas en attendant la fermeture du jeu : le bouton "Jouer" redevient disponible
dès que le process a démarré, et les logs du jeu remontent dans le journal de statut tant que la
fenêtre reste ouverte.

## Build

Le projet cible `net8.0-windows` (WPF) : à builder/exécuter sous Windows avec le SDK .NET 8.

```powershell
dotnet restore
dotnet build
dotnet run --project src/MinecraftLauncherPerso
```

> **Cet environnement de développement est Linux** et ne peut pas compiler de projet WPF
> (`net8.0-windows`), ni installer le SDK .NET ici : le code n'a donc pas encore été
> buildé/testé. À valider avec `dotnet build` sur une machine Windows.
>
> Les noms exacts de types/propriétés CmlLib.Core (`MSession`, `MLaunchOption.JavaPath`,
> `MinimumRamMb`/`MaximumRamMb`, `ForgeInstaller.Install(...)`, `ForgeInstallOptions`,
> `ProcessWrapper`) ont été vérifiés contre le code source et les exemples officiels des dépôts
> CmlLib.Core / CmlLib.Core.Installer.Forge, mais pas contre une compilation réelle : si `dotnet
> build` signale une propriété introuvable, corrigez le nom exact indiqué par le compilateur (le
> reste de la logique — orchestration, manifeste, lecture de launcher_accounts.json — n'en dépend
> pas).

## Configuration avant premier lancement

Modifier `%AppData%/MinecraftLauncherPerso/settings.json` (créé au premier lancement avec les
valeurs par défaut) pour renseigner au minimum `ModsServerBaseUrl` (URL de votre VPS où est
hébergé `manifest.json`).
