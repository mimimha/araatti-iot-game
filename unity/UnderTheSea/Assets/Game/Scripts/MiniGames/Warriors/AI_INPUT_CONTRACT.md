# Warriors AI input contract

The classifier and transport are external to Unity gameplay. Send one prediction per completed motion window to `WarriorsAIInputAdapter.OnActionPredicted`.

| Field | Type | Rule |
| --- | --- | --- |
| `PlayerId` | integer | `0` through `3` |
| `AttackType` | string/enum name | `None`, `HorizontalSlash`, `VerticalSlash`, `Thrust` |
| `Confidence` | float | normalized `0..1` |
| `Strength` | float | optional normalized `0..1`; use `1` when unavailable |
| `Timestamp` | double | monotonically increasing sensor or inference time per player |

Example: `P1 / HorizontalSlash / 0.91 / 0.82 / 1725955200.125`

Do not exchange the enum's numeric value. `None` and predictions below `MinConfidence` are ignored. Repeated or out-of-order timestamps inside the configured cooldown are ignored. BLE, Wi-Fi, UDP, Python, or an in-process model may be used later as long as it calls this contract.
