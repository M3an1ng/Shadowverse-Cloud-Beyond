## Context
Avatar images should follow the same storage and static-file behavior as card art assets.
The client needs a fixed-size crop frame with proportional zoom and a simple upload flow.

## Goals / Non-Goals
- Goals: consistent storage path, image type validation, fixed square crop UI, account-bound avatar URL.
- Non-Goals: advanced editing (rotation/filters), multi-image galleries, third-party storage.

## Decisions
- Store avatars under /assets/avatars/<userId>/avatar.<ext> (mirrors card art storage approach).
- Use a single fixed-size square crop output (320x320) and client-side scaling via canvas.
- Use the same image type whitelist as card art uploads.
- Display the avatar using an <img> element in the user-info-card.
- Update User.AvatarUrl and refresh local session display after upload.

## Risks / Trade-offs
- Client-side cropping relies on browser APIs; large files may be memory-heavy.
- Without server-side authentication, the endpoint must trust the provided userId (same as current user flow).

## Migration Plan
- No migration needed; AvatarUrl is already present and optional.

## Open Questions
- Max file size enforcement (should we add a hard limit server-side?).
