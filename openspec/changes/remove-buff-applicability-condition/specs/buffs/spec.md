## ADDED Requirements
### Requirement: Buff Applicability Ignores ApplicabilityCondition
The system SHALL treat buff applicability as a function of IsActive only and SHALL NOT evaluate ApplicabilityCondition at runtime.

#### Scenario: ApplicabilityCondition does not gate buff effects
- **WHEN** a buff has ApplicabilityCondition configured
- **THEN** the buff is still applicable as long as IsActive is true
