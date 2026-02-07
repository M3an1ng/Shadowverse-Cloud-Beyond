## 1. Backend
- [x] 1.1 Add `IsEnabled` to `TargetConfig` and `EffectConfig` with default true.
- [x] 1.2 Skip disabled targets during resolution and availability checks (mark resolved empty).
- [x] 1.3 Skip disabled effects during effect creation.

## 2. Frontend
- [x] 2.1 Add `isEnabled` defaults for new targets/effects and normalize existing data.
- [x] 2.2 Add compact enable/disable toggles to target/effect headers.
- [x] 2.3 Skip disabled targets/effects in editor validation.

## 3. Verification
- [ ] 3.1 Add or update tests if coverage exists for ability config validation (optional).
