# ProjectCostForecast Phase 3 Markdown Package

This package contains the Codex-ready Markdown files for the ProjectCostForecast Alpha release plan.

## Folder structure

```text
master/ProjectCostForecast_Master_Spec.md
alphas/Alpha_Index.md
alphas/Alpha_*.md
images/*.png
images/image_index.md
```

## Recommended Codex workflow

For each build, give Codex the master spec plus exactly one Alpha file. Example:

```text
Read docs/master/ProjectCostForecast_Master_Spec.md.
Read docs/alphas/Alpha_1_1_Forecast_Calculation_Period_Warning_and_Save_Trust.md.
Implement only Alpha 1.1.
Use screenshots from docs/images only where referenced by the Alpha file.
```

## Model rule

- Codex GPT-5.3: really basic / low-risk UI polish.
- Codex GPT-5.4: default model for normal WPF UI and medium-complexity work.
- Codex GPT-5.5: high-risk calculation, persistence, import, schedule, grid performance, architecture, resource calculation and complex curve work.

No Alpha in this package contains more than six active tasks.
