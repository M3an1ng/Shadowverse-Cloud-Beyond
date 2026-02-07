## ADDED Requirements
### Requirement: Avatar Upload Endpoint
The system SHALL allow a logged-in user to upload a new avatar image.

#### Scenario: Upload avatar successfully
- **WHEN** the user uploads a supported image file
- **THEN** the image is stored under the avatar assets path and the user profile is updated

#### Scenario: Unsupported image type
- **WHEN** the user uploads an unsupported file type
- **THEN** the upload is rejected with a clear error

### Requirement: Avatar Crop UI
The client SHALL provide a fixed-size square crop frame with proportional zoom before upload.

#### Scenario: Crop and upload
- **WHEN** the user selects an image and adjusts the crop and zoom
- **THEN** the client uploads a cropped square image to the avatar endpoint

### Requirement: Avatar Display Update
The client SHALL display the uploaded avatar for the current user session.

#### Scenario: Refresh avatar after upload
- **WHEN** the upload completes successfully
- **THEN** the user-info avatar updates to the new image
