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

Pas d'implémentation OAuth Microsoft pour l'instant (voir note ci-dessous) : le launcher lit la
session déjà authentifiée du launcher officiel. Pas de gestion multi-comptes : usage privé entre
amis, un seul compte par machine.

> **Note (2026-09) :** une version avec authentification OAuth Microsoft directe (MSAL.NET +
> Xbox Live/XSTS, sans dépendre du launcher officiel) a été développée et testée, mais Minecraft
> exige désormais une **approbation manuelle par Microsoft** de toute nouvelle application Azure AD
> avant d'autoriser l'appel à `login_with_xbox` (formulaire : https://aka.ms/mce-reviewappid,
> délai variable — de 24h à plusieurs mois selon les témoignages). En attendant cette approbation,
> le launcher est revenu à la lecture de la session du launcher officiel ci-dessous. Le code OAuth
> complet reste dans l'historique git (commits jusqu'à `961269e` / `5276172` sur la branche
> `claude/minecraft-launcher-csharp-942eie`) et pourra être restauré une fois l'app approuvée.

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
│           ├── ModSync/                    # synchro du modpack .zip depuis le VPS (ETag/Last-Modified)
│           │   ├── IModSyncService.cs
│           │   └── ModSyncService.cs
│           ├── Auth/                       # lecture session launcher officiel (launcher_accounts.json)
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

Le launcher pointe sur une archive `.zip` unique du pack complet, hébergée sur le VPS
(`ModpackZipUrl`), qui doit
contenir `mods/` et `config/` **à sa racine** (mêmes noms de dossiers qu'un `.minecraft`
classique) : à chaque mise à jour du pack, remplacez ce zip sur le VPS.

Avant de (re)télécharger, le launcher envoie une requête HTTP `HEAD` sur cette URL et compare
`ETag`/`Last-Modified`/`Content-Length` à la dernière synchro réussie (mise en cache localement
dans `{GameDirectory}/launcher-modpack-cache.json`) : si rien n'a changé côté serveur, il ne
retélécharge pas à chaque lancement. Si le serveur ne renvoie pas ces en-têtes (ou ne supporte pas
`HEAD`), le launcher retélécharge par prudence plutôt que d'échouer. Le zip est ensuite extrait
directement dans `GameDirectory`, en écrasant les fichiers existants.

## Authentification (session du launcher officiel)

Fichier : `src/MinecraftLauncherPerso/Services/Auth/MinecraftAuthService.cs`

Deux variantes du launcher officiel existent, avec des noms de fichiers différents mais le même
format JSON — les deux sont essayées dans `%AppData%/.minecraft/`, dans cet ordre :
1. `launcher_accounts_microsoft_store.json` (launcher installé depuis le **Microsoft Store / app
   Xbox** — la variante la plus courante sur une installation Windows récente).
2. `launcher_accounts.json` (launcher classique téléchargé sur minecraft.net).

Pour chaque fichier trouvé : cherche le compte référencé par `activeAccountLocalId`, et en extrait
`accessToken` + `minecraftProfile.name`/`.id` (pseudo et UUID réellement utilisés en jeu — le champ
racine `username` du compte est l'identifiant Microsoft/email, pas le pseudo Minecraft). Lève une
erreur explicite listant ce qui a été essayé si aucun des deux fichiers ne donne de session valide
(absent, aucun compte actif, ou token expiré) — dans ce cas, l'utilisateur doit ouvrir/rouvrir le
launcher officiel et se (re)connecter avant de relancer ce launcher.

**Chaque joueur doit donc avoir installé le launcher officiel Minecraft et s'y être connecté au
moins une fois** (ce qu'il doit de toute façon faire pour un compte légitime) avant d'utiliser ce
launcher.

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

> Le développement se fait dans un environnement Linux, qui ne peut pas compiler de projet WPF
> (`net8.0-windows`) : impossible de builder ou tester ici. Un workflow CI GitHub Actions
> (`.github/workflows/build-windows.yml`) build le projet sur `windows-latest` à chaque push et
> publie un exécutable en artifact — c'est le moyen de vérifier qu'un changement compile toujours.
>
## Configuration avant premier lancement

`ModpackZipUrl` est déjà préconfiguré par défaut (URL du VPS). Pour ajuster RAM, URL du modpack ou
dossier de jeu sans passer par l'UI, modifier `%AppData%/MinecraftLauncherPerso/settings.json`
(créé au premier lancement).
