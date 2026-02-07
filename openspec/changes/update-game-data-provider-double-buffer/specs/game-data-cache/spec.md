## ADDED Requirements
### Requirement: Atomic Cache Refresh
The system SHALL refresh GameDataProvider caches by swapping in a fully built snapshot so runtime reads never observe empty caches during refresh.

#### Scenario: Refresh in progress
- **GIVEN** a refresh is triggered while requests are reading card definitions
- **WHEN** a cache read occurs during the refresh
- **THEN** the read MUST return data from the previous snapshot until the new snapshot is fully loaded
