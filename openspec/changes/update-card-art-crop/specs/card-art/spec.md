## ADDED Requirements
### Requirement: Card art preview crop
The system SHALL provide a crop UI for preview art uploads in the card editor with a fixed 848:1024 aspect ratio and export a 848x1024 image.

#### Scenario: Preview art crop
- **WHEN** a user uploads preview art in the card editor
- **THEN** the crop box maintains a 848:1024 aspect ratio with a resize handle
- **AND** the uploaded image is exported at 848x1024 pixels

### Requirement: Card art detail crop
The system SHALL provide a freeform crop UI for detail art uploads and export the crop region at its original pixel dimensions.

#### Scenario: Detail art crop
- **WHEN** a user uploads detail art in the card editor
- **THEN** the crop box can be resized without a fixed aspect ratio
- **AND** the uploaded image is exported using the selected region's source pixel size