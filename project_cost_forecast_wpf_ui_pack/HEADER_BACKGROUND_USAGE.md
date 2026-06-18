# Header Background Assets

Add one of these behind the title and KPI cards in the top header area.

## Files

- `Assets/Backgrounds/bg_header_lake_soft_1600x220.png` — primary header background, matches the mockup lake/dam style.
- `Assets/Backgrounds/bg_header_lake_soft_3200x440.png` — high-DPI/@2x version.
- `Assets/Backgrounds/bg_header_gradient_1600x220.png` — clean fallback if you do not want a photographic header.

## Recommended WPF element name

`HeaderBackgroundImage`

## Suggested WPF placement

Place this as the first child inside the main `HeaderHero` Grid, behind the title row and KPI metric cards.

```xml
<Grid x:Name="HeaderHero" Height="220">
    <Image x:Name="HeaderBackgroundImage"
           Source="Assets/Backgrounds/bg_header_lake_soft_1600x220.png"
           Stretch="UniformToFill"
           Opacity="0.95" />

    <!-- Header title, period selector, validation text and KPI cards go above here -->
</Grid>
```

## Codex instruction

Use `bg_header_lake_soft_1600x220.png` as the hero/header background. It should fill the top content header area from the main content left edge to the right edge, behind the page title and KPI cards. The image should not sit behind the left navigation rail. Use `Stretch=UniformToFill`, clip overflow, and overlay a subtle white gradient if text contrast is low.
