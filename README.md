# IntTech Controller — Backend

The backend API for the **IntTech UI** app: the tool clinicians and Clinical Technology (ClinTech) staff use to control LUMOplay interactive projection systems and projectors at BC Children's Hospital (BCCH).

It is a **.NET 8 ASP.NET Core Web API** backed by **MongoDB**. The IntTech UI client (a Flutter app, in a **separate repository**) talks to this API; this API in turn talks to the LUMOplay computers and the projectors on the BCH - CI network.

> **New to this project?** Read the *Developer Orientation Guide* first — it explains what the system is for and how to get productive in your first week. This README covers setup and operation only.

---

## What this service does

| Area | Responsibility |
|---|---|
| **Authentication** | Username/password login, issues a JSON Web Token (JWT) |
| **Users & access** | Admin-managed accounts, per-location and per-tag access control |
| **Game library** | Catalog of games across three platforms: `lumoplay`, `vr`, `switch` |
| **Tags & categories** | Structured labels used both for browsing and for access control |
| **Playlists** | Personal and published ("default") ordered lists of games |
| **Devices** | Registered LUMOplay machines, identified by IP address |
| **Playback** | Start/stop a game, run a playlist, skip forward/back |
| **Projectors** | Power on/off, input discovery and switching over PJLink |
| **FAQ** | Editable help content served to the app |

---

## Prerequisites

Install these before you start. Everything below assumes **Windows** — see the note after the list.

1. **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>
2. **MongoDB Community Server** running locally on the default port `27017` — <https://www.mongodb.com/try/download/community>
3. **MongoDB Compass** (optional but strongly recommended) — a graphical browser for the database
4. **Visual Studio 2022** or **Visual Studio Code** with the C# Dev Kit extension
5. **Git**

> **Important — the LUMOplay tool is Windows-only.**
> Playback commands work by running LUMOplay's `MotionPlayer.Scripting.exe`, which only exists on a Windows machine with LUMOplay installed. On macOS or Linux the API will build and run and every other endpoint will work, but any `/api/Playback/*` call will return `Error: Scripting tool not found on Server`. Treat non-Windows as read-only development.

---

## First-time setup

1. **Clone the repository and open the backend folder.**

   ```bash
   git clone <REPOSITORY_URL>
   cd LumoPlayWrapper/IntTech_Controller_Backend
   ```


2. **Confirm MongoDB is running.** On Windows, open Services and check that `MongoDB Server` is Running, or open MongoDB Compass and connect to `mongodb://localhost:27017`. The database `inttech_controller` is created automatically on first run — you do not need to create it yourself.

3. **Restore dependencies.**

   ```bash
   dotnet restore
   ```

4. **Check `appsettings.json`.** The defaults are:

   ```json
   {
     "Lumo": {
       "ToolPath": "C:\\Program Files\\LUMOplay\\MotionPlayer.Scripting.exe",
       "DefaultPort": 5000
     },
     "ConnectionStrings": {
       "MongoDb": "mongodb://localhost:27017"
     }
   }
   ```

   If LUMOplay is installed somewhere else on your machine, update `Lumo:ToolPath` to the real path.

5. **Run the API.**

   ```bash
   dotnet run
   ```

   It listens on **`http://0.0.0.0:5221`** — reachable at `http://localhost:5221` on the same machine, and at `http://<machine-ip>:5221` from other devices on the same network.

6. **Open Swagger** at `http://localhost:5221/swagger` to browse and test every endpoint interactively.

   > **Note:** Swagger is only enabled when the app runs in the **Development** environment. In Production it is not served. `dotnet run` uses Development by default.

---

## First login

On very first startup, if the `users` collection is empty, the app seeds a master administrator:

```
username: admin
password: admin
```

> **Important:** Change this password before any deployment. The seed only runs when there are **no** users at all, so it will not re-create or reset the account later.

To get a token:

1. `POST /api/Auth/login` with `{ "username": "admin", "password": "admin" }`
2. Copy the `token` from the response.
3. In Swagger, click **Authorize** and enter `Bearer <token>`. Every other endpoint requires this.

Tokens are valid for **7 days**, but see *Session invalidation* in `docs/Game_Access_Model.md` — they can be rejected sooner.

---

## Project layout

```
IntTech_Controller_Backend/
├── Controllers/          One file per API area (Auth, Users, Games, Tag,
│                         Category, Location, Devices, Playback, Projector, Faq)
├── Models/               Database entities and data transfer objects (DTOs)
├── Data/
│   └── IntTechDBContext  EF Core context; maps each entity to a Mongo collection
├── Helpers/              ClaimsHelper, GameAccessHelper, GameFileStorage,
│                         SessionVersionMiddleware, ObjectIdConverter
├── Services/             LumoCommandService, ProjectorCommandService
├── wwwroot/              Uploaded game images and one-pagers (served statically)
├── docs/                 Backend_API_Reference.md, Game_Access_Model.md
├── appsettings.json      Configuration
└── Program.cs            Startup: services, auth, seeding, middleware, run
```

---

## Configuration reference

| Setting | Where | Default | What it does |
|---|---|---|---|
| `ConnectionStrings:MongoDb` | `appsettings.json` | `mongodb://localhost:27017` | MongoDB server address |
| Database name | Hard-coded in `Program.cs` | `inttech_controller` | Not configurable |
| `Lumo:ToolPath` | `appsettings.json` | `C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe` | Path to the LUMOplay command-line tool |
| `Lumo:DefaultPort` | `appsettings.json` | `5000` | Present in config; not currently read by the code |
| `Jwt:Key` | **Not set** | Falls back to a hard-coded string in `Program.cs` and `AuthController.cs` | Signing key for tokens |
| Listen address | Hard-coded in `Program.cs` | `http://0.0.0.0:5221` | Port and interfaces |

> **Important — the JWT signing key.** `Jwt:Key` is not present in `appsettings.json`, so the application falls back to a signing key written directly into the source code. Anyone who can read the repository can forge a valid token. Before this is deployed anywhere beyond a closed test network, set `Jwt:Key` to a long random value supplied outside source control (user secrets or an environment variable). See *Known limitations* in the Architecture document.

---

## Deployment

Production runs on a **dedicated Windows NUC** (small form-factor PC) connected to the **BCH-CI** network. The application is published as a build and that build is what runs on the NUC — the source tree and the .NET SDK are not needed on the server.

To deploy a change:

1. **Publish the build.**

   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Stop the running application** on the NUC.
3. **Copy the contents of `publish/` to the NUC**, replacing the previous build. Leave `wwwroot/` in place — it holds uploaded game images that are not in source control.
4. **Confirm `appsettings.json` on the NUC** still points at the correct MongoDB connection string and LUMOplay tool path. A published build will overwrite it with the repository copy if you are not careful.
5. **Start the application** and confirm the startup log is clean.
6. **Smoke-test** one login, one device list, and one playback command against a device that is not in clinical use.

> **Important:** The NUC must have LUMOplay installed, and must be able to reach the devices and projectors on the network. All hardware traffic originates from this one machine, not from the tablets. There is no continuous deployment pipeline; this is a manual copy-and-restart.

> **Note:** There is currently **no backup** of the MongoDB database or of `wwwroot/`. A proposed backup procedure is set out in the Architecture document under *Backup and recovery*.

---

## Common tasks

**Add a device (LUMOplay machine).** `POST /api/Devices` as an admin, with the machine's name, IP address, LUMOplay security key, and location ID. The security key comes from the LUMOplay application on that machine.

**Add a projector.** `POST /api/Projector` as an admin, then `POST /api/Projector/{id}/discover-inputs` while the projector is **powered on**. Discovery only works on a projector that is on and reachable; otherwise it returns 503 and leaves the stored inputs alone.

**Add a game.** `POST /api/Games` as an admin. For `lumoplay` games the `gameId` is required and must match the scene ID that LUMOplay uses — this is the value sent to the device. For `vr` and `switch` games the ID is generated automatically.

**Upload a game image.** `POST /api/Games/{gameId}/image` as multipart form data. The file is saved into `wwwroot/images/` and served at `/images/<filename>`.

Full endpoint documentation is in [`docs/Backend_API_Reference.md`](docs/Backend_API_Reference.md).

---

## Troubleshooting

| Symptom | Likely cause and fix |
|---|---|
| App exits at startup with a Mongo connection error | MongoDB is not running. Start the MongoDB service and retry. |
| Every request returns **401 Unauthorized** | Missing or expired token, or the `Authorization` header is missing the `Bearer ` prefix. Log in again. |
| Requests suddenly return 401 with *"Session expired. Please log in again."* | An admin edited your user account, or deleted a tag you held. This is intentional — log in again to pick up the new permissions. |
| Playback returns `Error: Scripting tool not found on Server` | `Lumo:ToolPath` points at a file that does not exist, or you are not on a Windows machine with LUMOplay installed. |
| Playback returns **502** *"Device command timed out or failed"* | The LUMOplay machine is off, unreachable, or its stored security key is wrong. Confirm you can ping the device IP, then re-check the key. |
| Projector shows as `offline` although it is plugged in | The backend could not open a TCP connection within 2 seconds. Check the IP, the port (PJLink default `4352`), and that the projector's network control is enabled. |
| Projector accepts no commands at all | The projector may have a PJLink password set. The service does not perform the PJLink authentication handshake, so control only works with PJLink authentication disabled. |
| Input switching returns **409 Conflict** | The projector is not powered on. Turn it on, wait for it to finish warming up, then retry. |
| A game you know exists returns **404** | Deliberate. Non-admin users get 404 (not 403) for games their tags do not permit, so the API never reveals that a hidden game exists. See `docs/Game_Access_Model.md`. |
| Duplicate playlist names appear for one user | The unique index `owner_name_unique` failed to create at startup. Check the startup log for the warning. |
| `/swagger` returns 404 | The app is running in Production. Swagger is Development-only. |

