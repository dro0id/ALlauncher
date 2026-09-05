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
4. Authentifie le joueur via OAuth Microsoft (device code flow) puis Xbox Live/XSTS — **implémenté**
5. Lance le jeu avec le bon classpath Forge et la RAM configurée — **implémenté**

Pas de gestion multi-comptes : usage privé entre amis, un compte Microsoft actif par session de jeu.

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
│           ├── Auth/                       # OAuth Microsoft (device code) -> Xbox Live -> XSTS -> Minecraft
│           │   ├── IAuthService.cs
│           │   └── MicrosoftAuthService.cs
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
(`ModpackZipUrl`, par défaut `http://185.185.82.180/modpack/Algaron-modded.zip`), qui doit
contenir `mods/` et `config/` **à sa racine** (mêmes noms de dossiers qu'un `.minecraft`
classique) : à chaque mise à jour du pack, remplacez ce zip sur le VPS.

Avant de (re)télécharger, le launcher envoie une requête HTTP `HEAD` sur cette URL et compare
`ETag`/`Last-Modified`/`Content-Length` à la dernière synchro réussie (mise en cache localement
dans `{GameDirectory}/launcher-modpack-cache.json`) : si rien n'a changé côté serveur, il ne
retélécharge pas à chaque lancement. Si le serveur ne renvoie pas ces en-têtes (ou ne supporte pas
`HEAD`), le launcher retélécharge par prudence plutôt que d'échouer. Le zip est ensuite extrait
directement dans `GameDirectory`, en écrasant les fichiers existants.

## Authentification (OAuth Microsoft)

Fichier : `src/MinecraftLauncherPerso/Services/Auth/MicrosoftAuthService.cs`

Le launcher ne dépend plus du launcher officiel Minecraft (ni de `launcher_accounts.json`) : il
s'authentifie lui-même directement auprès de Microsoft, via MSAL.NET (device code flow), puis
échange le token obtenu contre une session Minecraft via la chaîne standard :

1. **Microsoft (MSAL.NET, device code flow)** : au premier lancement (ou si la session en cache a
   expiré), le launcher affiche un message du type *"ouvrez https://microsoft.com/link et entrez le
   code ABCD-EFGH"* dans le journal de statut, et ouvre automatiquement le navigateur par défaut sur
   cette page. Une fois connecté dans le navigateur, le launcher récupère le token Microsoft.
2. **Xbox Live** (`user.auth.xboxlive.com/user/authenticate`) puis **XSTS**
   (`xsts.auth.xboxlive.com/xsts/authorize`) : échange le token Microsoft contre un token Xbox Live
   autorisé pour Minecraft.
3. **Minecraft Services** (`api.minecraftservices.com/authentication/login_with_xbox`) : échange le
   token XSTS contre le token d'accès Minecraft, puis récupère le pseudo/UUID réels via
   `/minecraft/profile`.

La session Microsoft (refresh token) est mise en cache localement
(`%AppData%/MinecraftLauncherPerso/msal-cache.bin`, via le mécanisme de sérialisation standard de
MSAL.NET) : une fois connecté une première fois, les lancements suivants renouvellent la session
**en silence**, sans redemander de connexion interactive, tant que le refresh token Microsoft reste
valide (généralement plusieurs mois).

Des erreurs Xbox Live courantes sont traduites en messages clairs (pas de compte Xbox associé,
compte enfant nécessitant une famille Microsoft, région non supportée, compte sans Minecraft) plutôt
que de simplement remonter un code d'erreur brut.

### Créer l'application Azure AD (obligatoire, à faire une seule fois)

Minecraft/Xbox Live n'acceptent que des tokens émis pour une application cliente explicitement
enregistrée : il n'existe pas de "client ID" générique réutilisable par un launcher tiers. Il faut
donc en créer une (gratuit, ~5 minutes) :

1. Aller sur [portal.azure.com](https://portal.azure.com) → **Azure Active Directory** (ou
   **Microsoft Entra ID**) → **App registrations** → **New registration**.
2. Nom libre (ex. "Launcher Algaron"), **Supported account types** = *"Personal Microsoft accounts
   only"*, pas de Redirect URI à cette étape → **Register**.
3. Copier l'**Application (client) ID** affiché sur la page — c'est la valeur à mettre dans
   `MicrosoftClientId` (voir plus bas).
4. Aller dans **Authentication** (menu de gauche) → **Add a platform** → **Mobile and desktop
   applications** → cocher `https://login.microsoftonline.com/common/oauth2/nativeclient` →
   **Configure**.
5. Toujours dans **Authentication**, activer **"Allow public client flows"** = **Yes**, puis
   **Save**.

Renseigner ensuite `MicrosoftClientId` avec ce client ID (voir section Configuration ci-dessous).

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
> Le flux OAuth Microsoft/Xbox Live/XSTS (`MicrosoftAuthService`) n'a en revanche encore jamais été
> testé en conditions réelles (nécessite un vrai compte Microsoft + une app Azure AD) : à valider
> en priorité après avoir créé l'app Azure AD (voir section Authentification).

## Configuration avant premier lancement

`ModpackZipUrl` est déjà préconfigué sur `http://185.185.82.180/modpack/Algaron-modded.zip` par
défaut. En revanche **`MicrosoftClientId` est vide par défaut et doit être renseigné avant de
pouvoir se connecter** (voir section Authentification ci-dessus pour le créer) : sans ça, le
launcher refuse de démarrer l'authentification avec un message explicite.

Modifier `%AppData%/MinecraftLauncherPerso/settings.json` (créé au premier lancement) :

```json
{
  "MicrosoftClientId": "<votre Application (client) ID Azure AD>"
}
```

Les autres champs (RAM, URL du modpack, dossier de jeu) peuvent aussi y être ajustés sans passer
par l'UI.
