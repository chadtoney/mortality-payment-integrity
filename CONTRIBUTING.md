# Contributing

## Ground rules

Keep this project investigation-only. Changes must not add automatic benefit actions, recovery actions, or writes to an agency system of record. Use synthetic data in code, tests, screenshots, issues, and pull requests.

Identity matching must remain deterministic and explainable. AI-assisted extraction or summarization must not become an authoritative identity or eligibility decision.

## Development

1. Create a focused branch.
2. Add or update tests for changed behavior.
3. Run the .NET tests and frontend checks.
4. Describe safety, privacy, and deployment implications in the pull request.

```powershell
dotnet test MortalityReview.slnx
Set-Location src/MortalityReview.Web
npm ci
npm run lint
npm run build
```

Do not commit secrets, real personal information, generated build output, or local Azure session files.