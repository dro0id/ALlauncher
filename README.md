# Launcher Minecraft personnalisé

Launcher WPF (.NET / C#) pour un serveur Minecraft privé (8 joueurs max), basé sur
[CmlLib.Core](https://github.com/CmlLib/CmlLib.Core).

## Contexte serveur

- Minecraft **1.16.5**, Forge **36.2.34**
- 87 mods (Botania, Create, Quark, Minecolonies, Twilight Forest, Biomes O'Plenty, ...)
- **Java 8 obligatoire** (Forge 1.16.5 est incompatible avec Java 11+)
- Mods/config hébergés en `.zip` sur un VPS perso, accessibles en HTTP direct

## Ce que fait le launcher

1. Vérifie/installe Java 8 (build Temurin/Adoptium si absent) — **implémenté**
2. Installe Forge 1.16.5-36.2.34 — à venir
3. Synchronise `mods/` et `config/` depuis le VPS (par hash, pas à chaque lancement) — à venir
4. Réutilise la session déjà connectée du launcher officiel (`launcher_accounts.json`) — à venir
5. Lance le jeu avec le bon classpath Forge et la RAM configurée — à venir

Pas d'implémentation OAuth Microsoft : le launcher lit la session déjà authentifiée sur la machine.
Pas de gestion multi-comptes : usage privé entre amis, un seul compte par machine.

## Structure du repo

```
├── MinecraftLauncherPerso.sln
├── src/
│   └── MinecraftLauncherPerso/
│       ├── MinecraftLauncherPerso.csproj   # net8.0-windows, WPF, référence CmlLib.Core
│       ├── App.xaml(.cs)                   # bootstrap de l'application WPF
│       ├── MainWindow.xaml(.cs)            # UI : bouton Jouer, barre de progression, champ RAM
│       ├── Models/
│       │   ├── LauncherSettings.cs         # préférences persistées (RAM, dossier de jeu, URL VPS)
│       │   ├── JavaVersionInfo.cs
│       │   └── JavaSetupProgress.cs
│       └── Services/
│           ├── Java/                       # détection + installation Java 8 (implémenté)
│           │   ├── IJavaManager.cs
│           │   ├── JavaManager.cs
│           │   └── AdoptiumApiClient.cs
│           ├── Forge/                      # installation Forge (squelette, à implémenter)
│           ├── ModSync/                    # synchro mods/config depuis le VPS (squelette)
│           ├── Auth/                       # lecture session launcher officiel (squelette)
│           ├── Launch/                     # lancement du jeu via CmlLib.Core (squelette)
│           └── Configuration/
│               └── SettingsManager.cs      # charge/sauvegarde settings.json
├── README.md
└── .gitignore
```

Chaque responsabilité (Java, Forge, sync mods, auth, lancement) est isolée dans son propre
service derrière une interface (`IJavaManager`, `IForgeManager`, `IModSyncService`,
`IAuthService`, `IGameLauncher`), pour pouvoir les implémenter et tester indépendamment.

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

`IJavaManager.EnsureJava8Async` rapporte sa progression via `IProgress<JavaSetupProgress>`
(étape courante, pourcentage, message), branché directement sur la barre de progression et le
journal de statut de `MainWindow`.

## Build

Le projet cible `net8.0-windows` (WPF) : à builder/exécuter sous Windows avec le SDK .NET 8.

```powershell
dotnet restore
dotnet build
dotnet run --project src/MinecraftLauncherPerso
```

> Cet environnement de développement est Linux et ne peut pas compiler de projet WPF
> (`net8.0-windows`) : le code n'a donc pas encore été buildé/testé. À valider avec
> `dotnet build` sur une machine Windows avant d'aller plus loin.

## Prochaines étapes

- `ForgeManager` : installation de Forge 1.16.5-36.2.34 via `CmlLib.Core.Installer.Forge` si
  supporté, sinon téléchargement + exécution silencieuse de l'installeur officiel.
- `ModSyncService` : manifeste JSON (fichier + hash SHA-256) comparé à un cache local pour ne
  télécharger que les fichiers modifiés depuis le VPS.
- `MinecraftAuthService` : lecture de `launcher_accounts.json` dans `.minecraft` (le format exact
  a changé avec la migration vers les comptes Microsoft ; à valider contre un fichier réel).
- `GameLauncher` : construction de la session CmlLib.Core, des options de lancement (RAM,
  chemin Java) et exécution du profil Forge installé.
