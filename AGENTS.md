# Just Some Stars agent handoff

Read `docs/tooling/agent-environment.md` before running builds, device tests, Blender work, or ShipKit integrations.

Task 0 configured the machine and agent services only. Do not begin Task 1 or gameplay implementation unless the user explicitly asks to continue.

- Storage rule: `/mnt/unity-data/JustSomeStars` is the canonical repository and work directory. Create and operate every game file there, including source, Unity `Library`, imported/generated assets, build artifacts and caches. Do not create a second active worktree on the system partition.
- Limrun is authenticated and its project-scoped Codex skills are under `.codex/skills/`.
- Argent 0.21.0 is installed, its MCP is declared in `.codex/config.toml`, and its skills are under `.agents/skills/`.
- Blender MCP uses port `9876`; Poly Haven is enabled.
- Never commit API keys, login tokens, signing material, store credentials, or the contents of `~/.lim/config.yaml`.
