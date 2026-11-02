# Blog and Gallery API examples

## Public blogs

`GET /api/Blog`

Returns an array of published blog records. Example shape:

```json
[
  {
    "blogId": 1,
    "title": "Your First Hot Air Balloon Ride",
    "slug": "your-first-hot-air-balloon-ride",
    "category": "Aero Sports",
    "excerpt": "A simple guide to your first balloon flight.",
    "content": "Full blog content...",
    "imageUrl": "https://example.com/balloon.jpg",
    "author": "Airborne Flight Desk",
    "isPublished": true,
    "publishedAt": "2026-10-29T09:00:00Z",
    "createdAt": "2026-10-29T09:00:00Z",
    "updatedAt": null
  }
]
```

## Public gallery

`GET /api/Gallery`

Returns active `Image` records from the existing media store. Each record includes `mediaId`, `mediaName`, `mediaUrl`, `isActive` and `createdDate`.

## Admin write request

Send the `X-Admin-Key` header with POST, PUT, DELETE, and `/api/Blog/admin/all`. Keep this value server-side; do not embed it into the public website bundle.
