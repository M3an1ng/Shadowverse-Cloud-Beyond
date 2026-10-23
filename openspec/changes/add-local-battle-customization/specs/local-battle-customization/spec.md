## ADDED Requirements

### Requirement: The client shall persist a custom local battle background per browser

The system SHALL allow a player to choose a supported local image file as a battle background and persist that asset in the current browser so it can be restored on later visits without re-uploading or reselecting the file.

#### Scenario: Saved background is restored after refresh

- **GIVEN** the player previously saved a valid custom battle background in the current browser
- **WHEN** the player refreshes the page and re-enters the game page
- **THEN** the system restores the saved background automatically
- **AND** the player does not need to reselect the image file

#### Scenario: Replacing the background overrides the prior saved asset

- **WHEN** the player selects a new valid custom battle background
- **THEN** the new image replaces the previously saved background asset
- **AND** future restores use the new image

#### Scenario: Clearing the background returns to default visuals

- **WHEN** the player clears the custom battle background
- **THEN** the saved background asset is removed from browser storage
- **AND** the game page uses the default built-in battle visuals

### Requirement: The client shall persist a custom local battle BGM per browser

The system SHALL allow a player to choose a supported local audio file as battle BGM and persist that asset in the current browser so it can be restored on later visits without re-uploading or reselecting the file.

#### Scenario: Saved BGM is restored after refresh

- **GIVEN** the player previously saved a valid custom battle BGM in the current browser
- **WHEN** the player refreshes the page and re-enters the game page
- **THEN** the system restores the saved BGM selection automatically
- **AND** the client attempts to apply the saved BGM for playback in the current session

#### Scenario: Replacing the BGM overrides the prior saved asset

- **WHEN** the player selects a new valid custom BGM file
- **THEN** the new audio asset replaces the previously saved BGM asset
- **AND** future restores use the new asset

#### Scenario: Clearing the BGM removes local playback customization

- **WHEN** the player clears the custom BGM
- **THEN** the saved BGM asset is removed from browser storage
- **AND** any currently playing custom BGM is stopped

### Requirement: Battle customization assets shall remain local-only

The system SHALL treat custom battle background and BGM assets as local browser customization only. The system SHALL NOT upload these assets to the backend, include them in shared game state, or synchronize them to other participants.

#### Scenario: Multiplayer match does not expose local background

- **WHEN** a player joins or plays a multiplayer match with a locally customized battle background
- **THEN** the backend receives no uploaded background asset from that customization
- **AND** other participants do not receive the player's local background asset

#### Scenario: Multiplayer match does not expose local BGM

- **WHEN** a player joins or plays a multiplayer match with a locally customized BGM
- **THEN** the backend receives no uploaded BGM asset from that customization
- **AND** other participants do not receive the player's local BGM asset

### Requirement: The client shall validate supported types and size limits before saving assets

The system SHALL reject unsupported or oversized local assets before they replace existing saved customization.

#### Scenario: Unsupported background type is rejected

- **WHEN** the player selects a file that is not one of `image/png`, `image/jpeg`, or `image/webp` for the battle background
- **THEN** the system rejects the selection
- **AND** the previously saved background, if any, remains unchanged
- **AND** the player sees a clear validation message

#### Scenario: Oversized background is rejected

- **WHEN** the player selects a supported image file larger than `10 MB` for the battle background
- **THEN** the system rejects the selection
- **AND** the previously saved background, if any, remains unchanged

#### Scenario: Unsupported BGM type is rejected

- **WHEN** the player selects a file that is not one of `audio/mpeg`, `audio/ogg`, `audio/wav`, or `audio/mp4` for battle BGM
- **THEN** the system rejects the selection
- **AND** the previously saved BGM, if any, remains unchanged
- **AND** the player sees a clear validation message

#### Scenario: Oversized BGM is rejected

- **WHEN** the player selects a supported audio file larger than `20 MB` for battle BGM
- **THEN** the system rejects the selection
- **AND** the previously saved BGM, if any, remains unchanged

### Requirement: The client shall degrade gracefully on restore or playback failure

The system SHALL recover safely when a persisted customization cannot be restored or when the browser blocks automatic audio playback.

#### Scenario: Broken background record falls back to default

- **GIVEN** a persisted custom battle background record is unreadable or cannot be rendered
- **WHEN** the system attempts to restore it
- **THEN** the system removes or invalidates the broken background record
- **AND** the game page falls back to default built-in visuals
- **AND** the player sees a non-blocking failure notice

#### Scenario: Broken BGM record falls back to default audio state

- **GIVEN** a persisted custom BGM record is unreadable or cannot be loaded
- **WHEN** the system attempts to restore it
- **THEN** the system removes or invalidates the broken BGM record
- **AND** the client falls back to default audio state without uncaught errors
- **AND** the player sees a non-blocking failure notice

#### Scenario: Browser blocks autoplay for restored BGM

- **GIVEN** the player has a valid saved custom BGM
- **WHEN** the browser rejects automatic playback because user interaction has not yet occurred
- **THEN** the saved BGM selection remains intact
- **AND** the client does not treat the asset as corrupted
- **AND** the UI exposes that playback can be retried after user interaction
