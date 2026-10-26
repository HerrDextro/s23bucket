# s23bucket – virtual filesystem design (agreed 20.10.2026)

## Core ideas
- Every user's tree starts at `/`. Username is NOT part of the stored path.
- Security = every query filters by `ownerId` (or `sharedWith` contains userId). The path is never the security barrier; the normalizer is for consistency.
- Store `parent` + `name` separately (not the full path). Listing a folder = exact match on `parent`. No regex on user input.
- Files and folders live in the same place (`fs.files`). Folder = zero-length GridFS upload, `type: "dir"`.
- Hidden files = dot-prefix convention, client-side only. Server ignores it.
- Sharing = per file only, no folder permissions, no inheritance. Owner can do everything.

## Document shape (fs.files)
```
filename:  "report.pdf"
metadata: {
  ownerId:     "<user id>"
  parent:      "/docs/work"        // "/" = root
  type:        "file" | "dir"
  contentType: "application/pdf"   // files only
  sharedWith:  [ { userId, canWrite } ]   // files only, optional
}
```
Unique index: `metadata.ownerId + metadata.parent + filename`.

## Path rules (static normalizer, no fields)
- Starts with `/`; root is `/`; no trailing `/` except root
- No empty segments, no `.` / `..`, no `\`
- Names: no `/`, no control chars, max 255
- Case-sensitive, stored as typed (no lowercasing)
- Invalid → 400, never "repair"

## Endpoints
| Method | Route | Does | Who |
|---|---|---|---|
| POST | /files | upload (multipart: file + parent) → 201 + id | owner |
| POST | /folders | create folder { parent, name } → 201 + id | owner |
| GET | /files?path=/docs | direct children [{ id, name, type, size, contentType, uploadDate }] | owner |
| GET | /files/{id} | stream download | owner or sharedWith |
| PUT | /files/{id} | replace content | owner or canWrite |
| DELETE | /files/{id} | delete; folders only if empty | owner |
| GET | /files/shared | flat list of files shared with me | any logged-in user |
| POST | /files/{id}/share | { username, canWrite } add/update entry | owner |

## Behaviour defaults
- Upload into a non-existing folder → 404 (alternative: auto-create parents)
- Name already exists → 409 (overwrite = PUT)
- Delete a non-empty folder → 409 (recursive delete = later)
- Rename/move: out of scope. If added: folder rename updates `parent` of all descendants via anchored, escaped prefix regex
- Not allowed to see it → 404 (not 403, which confirms existence)
- Invalid ObjectId string → 400 (`ObjectId.TryParse`)

## Implementation notes
- One permission method: (file metadata, userId, action) → bool. Every single-file endpoint calls it. List endpoints rely on their query filter.
- Upload: ownerId from the `NameIdentifier` claim
- Mongo nested fields use dots: `"metadata.ownerId"` (not `:` like config)
- `_bucket.FindAsync` for queries; raw `fs.files` collection (`GetCollection<BsonDocument>("fs.files")`) for metadata updates (sharing, later moves)
- Metadata fields may be missing when read back → check before reading, or deserialize into a model class
- `file.ContentType` is client-supplied → don't trust it when serving downloads
- Antiforgery disabled on upload is correct: bearer token in header = no CSRF risk
- isPublic dropped; if it returns later → a special entry in `sharedWith`
