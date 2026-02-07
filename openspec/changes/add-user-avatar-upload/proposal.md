# Change: Add user avatar upload and crop

## Why
Users need a personalized avatar and a consistent, predictable way to upload and crop images.

## What Changes
- Add avatar upload endpoint using the same storage pattern as card art assets.
- Add client-side avatar cropping with a fixed square frame and zoom control.
- Persist avatar URL on the user record and update the user-info-card display.

## Impact
- Affected specs: user-accounts (modified)
- Affected code: Server user/profile endpoints, static assets, user-info-card UI
