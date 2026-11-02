# Airborne Aviation API — Existing backend + Blogs + Gallery

This project extends the supplied ASP.NET Core Web API. Existing Banner, Enquiry and Media endpoints are retained. New endpoints are added for public blog listing/detail and public active-image gallery listing/detail.

## Requirements

- .NET SDK 10 (the supplied project targets `net10.0`)
- SQL Server / SSMS
- Existing `AirborneDB` database and the objects already used by the original project:
  - `dbo.EnquiryMaster`
  - `dbo.SP_ManageEnquiry`
  - `dbo.ManageMedia` and the media table it manages
- Cloudinary account for Media upload/delete endpoints

> The supplied archive did not include the SQL definitions for the existing Enquiry/Media objects. This package preserves those APIs and adds a separate blog database script. Do not drop or replace the existing tables/procedures.

## 1. Configure SQL Server

Open `Database/BlogDatabase.sql` in SSMS and execute it against your existing `AirborneDB`. It creates `BlogMaster` and these stored procedures:

- `dbo.GetPublishedBlogs`
- `dbo.GetPublishedBlogBySlug`
- `dbo.GetAllBlogs`
- `dbo.CreateBlog`
- `dbo.UpdateBlog`
- `dbo.DeleteBlog`

Change `ConnectionStrings:DefaultConnection` in `appsettings.json` to your own SQL Server instance, or override it with the `ConnectionStrings__DefaultConnection` environment variable. Do not commit real production connection strings.

## 2. Configure Cloudinary and blog admin key

The original RAR contained a live Cloudinary secret. It has been removed from this package. Rotate/revoke that secret in Cloudinary and use the replacement only in your local environment/secret manager.

In PowerShell, set the following environment variables before starting Visual Studio or `dotnet run`:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=.;Database=AirborneDB;Trusted_Connection=True;TrustServerCertificate=True;"
$env:Cloudinary__CloudName = "YOUR_CLOUD_NAME"
$env:Cloudinary__ApiKey = "YOUR_CLOUDINARY_API_KEY"
$env:Cloudinary__ApiSecret = "YOUR_NEW_CLOUDINARY_SECRET"
$env:BlogAdmin__ApiKey = "SET_A_LONG_RANDOM_SECRET_FOR_BLOG_ADMIN_ENDPOINTS"
dotnet run
```

Choose a long, random `BlogAdmin__ApiKey`. Never place this key in the public Netlify JavaScript. It is only for a trusted admin tool or API client. Blog write/admin endpoints return `503` until the server key is configured and `401` when `X-Admin-Key` is missing or incorrect.

If using Visual Studio, configure these values in your local launch profile or user secrets instead of committing them into `appsettings.json`.

## 3. Run and open Swagger

Run the `WebApplication1` project. Swagger is enabled. The provided HTTPS launch URL is normally `https://localhost:7049/swagger`; check `Properties/launchSettings.json` if your port differs.

## Existing endpoints retained

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/Banner` | Banner text and image URLs |
| POST | `/api/Enquiry` | Submit the contact/enquiry form |
| GET | `/api/Enquiry` | List enquiries (existing admin API; secure before production) |
| PUT | `/api/Enquiry/{id}` | Update enquiry (existing API; secure before production) |
| DELETE | `/api/Enquiry/{id}` | Delete enquiry (existing API; secure before production) |
| GET | `/api/Media/active` | Existing active image/video media |
| POST | `/api/Media/upload` | Existing Cloudinary upload (`mediaType` = `Image` or `Video`) |
| PATCH | `/api/Media/{id}/status` | Activate/deactivate media |
| DELETE | `/api/Media/{id}` | Delete media and Cloudinary asset |

The original Enquiry and Media management endpoints were retained for compatibility. They do not implement authentication in the supplied project; do not expose their admin operations publicly without adding authentication/authorization.

## New Blog endpoints

Public read endpoints:

- `GET /api/Blog` — published blogs only
- `GET /api/Blog/{slug}` — one published blog by slug, e.g. `/api/Blog/your-first-balloon-ride`

Admin endpoints require header `X-Admin-Key: <your server-side BlogAdmin__ApiKey>`:

- `GET /api/Blog/admin/all` — all posts, including drafts
- `POST /api/Blog` — create a post
- `PUT /api/Blog/{id}` — update a post
- `DELETE /api/Blog/{id}` — delete a post

Example POST body:

```json
{
  "title": "Your First Hot Air Balloon Ride",
  "slug": "your-first-hot-air-balloon-ride",
  "category": "Aero Sports",
  "excerpt": "A simple guide to your first balloon flight.",
  "content": "Full blog article content goes here.",
  "imageUrl": "https://your-image-host.example/blog/balloon.jpg",
  "author": "Airborne Flight Desk",
  "isPublished": true
}
```

Use unique URL-friendly slugs (lowercase words separated by hyphens). Set `isPublished` to `false` to save a draft. Only published posts are returned by the public endpoints. If your website renders blog content as HTML, sanitize it before inserting it into the page; do not assign untrusted article content directly to `innerHTML`.

## New Gallery endpoints

- `GET /api/Gallery` — active images only; excludes videos and inactive media
- `GET /api/Gallery/{id}` — one active gallery image

Gallery images are managed through the existing Media API:

- `POST /api/Media/upload` with `multipart/form-data`: `mediaName`, `mediaType=Image`, `file`
- `PATCH /api/Media/{id}/status` with JSON `{ "isActive": false }` to hide an image
- `DELETE /api/Media/{id}` to delete it

## Netlify frontend integration

The API must be deployed to a publicly reachable HTTPS address before the Netlify website can call it. CORS already allows `https://airborne-aviation-web.netlify.app` and local development on port 5500. Replace the frontend's API base URL with your deployed API URL; do not use `localhost` in the deployed website.

Example public blog fetch:

```javascript
const API_BASE_URL = "https://YOUR-DEPLOYED-API-DOMAIN";
const response = await fetch(`${API_BASE_URL}/api/Blog`);
if (!response.ok) throw new Error("Blogs could not be loaded");
const blogs = await response.json();
```

Example gallery fetch:

```javascript
const response = await fetch(`${API_BASE_URL}/api/Gallery`);
if (!response.ok) throw new Error("Gallery could not be loaded");
const images = await response.json();
```

## Notes

- Blog data is stored in SQL Server using stored procedures and parameterized commands.
- Gallery uses the existing `dbo.ManageMedia` procedure and existing Cloudinary media records.
- `appsettings.json` contains placeholders only; configure secrets on your machine/server.
- Test database connectivity and run the SQL script before testing Blog/Gallery APIs.

## Authentication APIs (Sign Up / Login / Password / Logout / Delete)

Run `Database/AuthDatabase.sql` against the database configured by `ConnectionStrings:DefaultConnection` before using these endpoints. It creates `dbo.AuthUsers` and `dbo.RevokedAuthTokens` and does not change existing tables.

Configure a long random JWT signing secret (at least 32 UTF-8 bytes) using environment variables or .NET user secrets; do not commit the secret:

```powershell
$env:Jwt__Secret = "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_BYTES"
$env:Jwt__Issuer = "AirborneAviationApi"
$env:Jwt__Audience = "AirborneAviationClient"
$env:Jwt__AccessTokenMinutes = "60"
dotnet run
```

### Endpoints

- `POST /api/Auth/signup` — JSON `{ "loginId": "puneet01", "fullName": "Puneet Sharma", "password": "UseA_StrongPassword123" }`
- `POST /api/Auth/login` — JSON `{ "loginId": "puneet01", "password": "UseA_StrongPassword123" }`; returns `accessToken`.
- `PUT /api/Auth/update-password` — authenticated; JSON `{ "currentPassword": "UseA_StrongPassword123", "newPassword": "Another_StrongPassword456" }`.
- `POST /api/Auth/logout` — authenticated; revokes the current token.
- `DELETE /api/Auth/delete-account` — authenticated; JSON `{ "password": "Another_StrongPassword456" }`.

For protected endpoints, send `Authorization: Bearer YOUR_ACCESS_TOKEN`. Passwords are stored as salted PBKDF2 hashes, never as plain text. JWTs are checked for expiry, signature, account existence, and revocation on authenticated requests. The database must be reachable for protected token validation. Password changes should be followed by a new login; existing access tokens are not automatically revoked by a password change, so keep their expiry short.

Swagger lists these endpoints. For protected endpoints, send `Authorization: Bearer YOUR_ACCESS_TOKEN` using your API client (for example, Postman).
