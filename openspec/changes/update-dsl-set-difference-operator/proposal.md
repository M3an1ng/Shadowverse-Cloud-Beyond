# Proposal: Update DSL Set-Difference Operator Symbol

## Summary
Replace the DSL set-difference operator currently overloaded on - with the dedicated symbol ~, to avoid ambiguity with numeric subtraction.

## Motivation
- - is overloaded for numeric subtraction and set difference, which causes parsing/typing ambiguity.
- A dedicated set-difference operator improves clarity and allows safe typing rules for collection operations.

## Scope
- DSL lexer/parser: recognize the new operator token.
- AST/operator mapping (front-end + back-end) for set-difference.
- Evaluation: map the new operator to set-difference logic.
- UI/editor: operator menus, previews, and AST mapping.
- No backward compatibility: legacy - set-difference is removed.

## Out of Scope
- Changing existing arithmetic semantics for -.
- Non-DSL expression languages.

## Requirements

### MODIFIED Requirements

#### Requirement: Set-Difference Symbol
The DSL must use a dedicated operator symbol ~ for set-difference.

#### Scenario: Parse Dedicated Set-Difference Operator
Given a DSL expression using the new operator symbol, the parser should produce a set-difference AST node.

#### Scenario: Evaluate Dedicated Set-Difference Operator
Given two collections, the evaluator should compute set-difference via the new operator symbol.

#### Scenario: Editor Displays Dedicated Operator
The DSL editor should show the new operator in menus and preview text.

### ADDED Requirements

#### Requirement: Legacy Compatibility
Legacy - set-difference is removed; - is only numeric subtraction.

## Open Questions
- None.

## Risks
- Breaking existing DSL expressions that rely on - for set-difference.
- Operator precedence interactions (must match current set-op precedence).

## Migration
- Provide guidance to update old expressions: replace - with ~ when the intent is set-difference.
