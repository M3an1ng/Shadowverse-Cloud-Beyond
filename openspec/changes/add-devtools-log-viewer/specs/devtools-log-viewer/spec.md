## ADDED Requirements
### Requirement: Dev-Tools Log Viewer
The system SHALL provide a /dev-tools page that renders a real-time log stream, is available only to logged-in users, and supports filter, search, auto-scroll, and JSON export.

#### Scenario: View real-time log stream
- **WHEN** a user opens /dev-tools
- **THEN** new log entries are displayed without manual refresh

#### Scenario: Filter by log level
- **WHEN** the user selects one or more log levels
- **THEN** only matching log entries are displayed

#### Scenario: Search by keyword
- **WHEN** the user enters a keyword query
- **THEN** only log entries containing the keyword are displayed

#### Scenario: Auto-scroll control
- **WHEN** auto-scroll is enabled
- **THEN** the view follows incoming log entries automatically

#### Scenario: Export logs
- **WHEN** the user triggers export
- **THEN** the currently displayed log entries are downloaded as a JSON file

#### Scenario: Access control
- **WHEN** a guest user opens /dev-tools
- **THEN** the log stream is not connected and a login prompt is shown
