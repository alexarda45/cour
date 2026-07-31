# ChromaBlast Project Guardrails

This is a Unity 6.5 Android portrait project. Preserve the exact installed project
version and the current `CanvasScaler` and safe-area behavior.

For Game Over UI work, do not modify unless absolutely required:

- gameplay mechanics, board rules, piece generation, scoring, combo/chain logic,
  or the POP mechanic
- the save system or existing `PlayerPrefs` keys
- rewarded ads or Unity LevelPlay integration
- `ProjectSettings`, `Packages`, the Input System, the Unity version, unrelated
  scenes, stable menus, or existing gameplay effects

Do not regenerate an entire scene or create duplicate canvases, event systems,
managers, button listeners, or save systems. Preserve working serialized
references and callbacks, especially the existing `RestartButton` callback.
Prefer disabling obsolete optional UI objects over deleting them.

Do not use legacy `UnityEngine.Input`, add packages or dependencies, place
`Image` and `TextMeshProUGUI` on the same GameObject, bake dynamic scores into
sprites, destroy working gameplay systems, or introduce compile errors or
console warnings.

Game Over changes must remain scoped to `Assets/Scenes/Game.unity`, the existing
Game Over UI/runtime script, the idempotent Game Over editor builder, and the
provided assets under `Assets/Art/Ocean/UI/GameOver/`.
