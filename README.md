# s23bucket

A self-hosted file bucket: an ASP.NET Core Minimal API that stores files in MongoDB (GridFS) behind a per-user virtual folder tree, with JWT authentication. Runs as two containers via Docker Compose.

> **Status: in progress.** Authentication and the core file API (upload, download, folders, listing, replace, delete) work and have been tested end to end. Sharing, renaming/moving and the external-service access flow are not finished. Not intended for deployment yet.

## Features

| Area | State |
|---|---|
| Register, login, refresh (JWT + refresh token, bcrypt password hashing) | ✅ works |
| Constant-time login (no timing difference between unknown user and wrong password) | ✅ works |
| Upload files, create folders, nested folders | ✅ works |
| Download by id or by path (streamed from GridFS) | ✅ works |
| List a folder's contents | ✅ works |
| Replace a file's content (PUT) | ✅ works |
| Delete files and folders (folders only when empty) | ✅ works |
| Per-user isolation (users can't see or touch each other's files) | ✅ works |
| Rename / move (PATCH) | ⬜ planned |
| Sharing files with other users | ⬜ planned |
| Validation of file and folder names (e.g. rejecting `a/b` or `..` as a name) | ⬜ planned |
| External services: temporary access tokens for their users | 🟨 partly built (see below) |
| Deployment (Raspberry Pi) | ⬜ planned |

## How it works

**Auth.** Passwords are hashed with bcrypt. Login returns a JWT (valid 60 minutes) and a refresh token stored in MongoDB (valid 7 days). Every file endpoint requires the JWT.

**Storage.** Files are stored with GridFS: MongoDB splits each file into chunks (`fs.chunks`) and keeps one document per file (`fs.files`). Each `fs.files` document carries the name plus metadata:

```
filename:  "report.pdf"
metadata: {
  ownerId:     "<user id>"     // every query filters on this
  parent:      "/docs/work"    // folder it lives in, "/" = root
  type:        0 | 1           // File | Dir
  contentType: "application/pdf"
  permission:  { ... }         // reserved for sharing
}
```

**Virtual folder tree.** There are no real directories. A folder is a zero-length GridFS entry with `type: Dir`. Listing `/docs` means "all entries owned by me whose `parent` is `/docs`". Every user's tree starts at `/`, and because every query includes the owner id, a path from another user's tree simply finds nothing. The path is never the security boundary, the owner filter is.

**Paths.** All paths go through one normalizer: they must start with `/`, have no trailing `/` (except root), and no empty, `.` or `..` segments. Invalid paths are rejected with 400, never "repaired".

**Replace.** `PUT /files` uploads the new content first and only then deletes the old entry, so a failed upload never loses the existing file. The file keeps its metadata but gets a new id.

**External services (in progress).** A registered service (API key stored as a bcrypt hash) can request a short-lived token for a set of resources. The request is kept in memory for one minute under a random id, which goes into the token as a claim. Not done yet: the request endpoint doesn't check the service's API key, and no endpoint accepts the client token yet.

## API

All file endpoints need `Authorization: Bearer <jwt>`.

| Method | Route | Body / query | Returns |
|---|---|---|---|
| POST | `/auth/register` | JSON `{ username, password }` | 201 |
| POST | `/auth/login` | JSON `{ username, password }` | 200 `{ token, refreshToken }` |
| POST | `/auth/refresh` | JSON `{ refreshToken }` | 200 `{ token }` |
| POST | `/files` | multipart: `File`, `ParentPath` | 201 `{ fileId }` |
| POST | `/folders` | JSON `{ parentPath, folderName }` | 201 `{ fileId }` |
| GET | `/files/{id}` | | file content |
| GET | `/files?path=/docs/a.txt` | | file content |
| GET | `/files/content?path=/docs` | | 200 `[{ id, name, type, size, uploadDate }]` |
| PUT | `/files` | multipart: `File`, `ParentPath` (replaces the file with that name) | 200 `{ fileId }` (new id) |
| DELETE | `/files/delete/{id}` | | 204 |
| DELETE | `/files/delete?path=/docs` | | 204 |
| POST | `/service/register` | JSON `{ name, url, api_key }` (needs a user login) | 201 |
| POST | `/service/request` | JSON `{ resource_id_permission: { ... } }` (no auth yet, in development) | 200 `{ token }` |

Errors: `400` malformed path or id, `404` not found or not yours (deliberately not 403, so the API doesn't confirm that someone else's file exists), `409` name already exists or folder not empty.

## Running it

**With Docker Compose.** Create a `.env` file next to `docker-compose.yml` (it's gitignored):

```
MONGO_ROOT_USER=<mongo user>
MONGO_ROOT_PASS=<mongo password>
JWT_SECRET=<random string, at least 32 bytes>
MongoDbSettings__ConnectionString=mongodb://<mongo user>:<mongo password>@mongodb:27017
```

Then run `docker compose up --build`. The API is at `http://localhost:5000`.

**Without Docker (Visual Studio, `http` profile).** Start only the database with `docker compose up mongodb`, and put the local values in user secrets (right-click the project → Manage User Secrets). Note `localhost` instead of `mongodb`, because the `mongodb` hostname only exists inside the Compose network:

```json
{
  "MongoDbSettings:ConnectionString": "mongodb://<mongo user>:<mongo password>@localhost:27017",
  "JwtSettings:SecretKey": "<random string, at least 32 bytes>"
}
```

Non-secret settings (database names, JWT issuer/audience, expiry times) live in `appsettings.json`.

## Project structure

```
cloud.api/
  Endpoints/   Minimal API route groups (Auth, Files, ExternalClient)
  Services/    FileService (GridFS), VirtualPath (path rules), UserService, AuthService, JWTService, ExternalClientService
  Models/      Database models and result types (ServiceResult<T>, ResultReturn, ...)
  Dtos/        Request bodies
  Settings/    Typed configuration (MongoSettings, JWTSettings)
```

## Roadmap

- PATCH for renaming and moving
- Sharing (`POST /files/{id}/share`, `GET /files/shared`)
- Name validation for files and folders
- External services: API key check on `/service/request`, an endpoint that accepts the client token
- Deployment on a Raspberry Pi (ARM image)

## History

s23bucket started in February 2026 as a school project (Lernperiode 9) in the repository `Lernperiode-9`, together with a Spectre.Console command-line client, which is not part of this repository. Between February and March I set up the Docker/MongoDB infrastructure, JWT auth with refresh tokens and bcrypt hashing, and the first version of the external-service idea. A token bug I couldn't track down led me to rewrite the auth code. After a break of about six months, I picked the project up again in October 2026: I stabilized the existing code (middleware order, constant-time login, moving secrets out of the repository), designed the virtual folder tree, and built the file endpoints.
