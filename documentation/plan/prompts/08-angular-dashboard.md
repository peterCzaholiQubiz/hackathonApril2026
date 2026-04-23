# Prompt 08 — Angular Dashboard

**Agent**: `general-purpose`  
**Phase**: 3 — AI + Frontend  
**Status**: TODO  
**Depends on**: Prompt 03 (Angular scaffold), Prompt 05 (API endpoints running)

---

Implement the Angular dashboard feature for the Customer Portfolio Thermometer.

Read documentation/plan.md (Frontend section) and documentation/plan/architecture.md.

Install ng2-charts and chart.js:
  npm install ng2-charts chart.js

COMPONENTS TO IMPLEMENT:

1. frontend/src/app/features/dashboard/dashboard.component.ts + .html + .scss
   - On init: fire 4 parallel API calls using forkJoin:
     GET /api/portfolio/current, /api/portfolio/segments,
     /api/risk/top-at-risk?type=overall&limit=10, /api/portfolio/history
   - Show LoadingSkeletonComponent while loading
   - Compose the four child components below

2. frontend/src/app/features/dashboard/portfolio-heatmap/portfolio-heatmap.component.ts
   - Doughnut chart (ng2-charts) showing green/yellow/red percentage
   - Colors: green=#22c55e, yellow=#f59e0b, red=#ef4444
   - Center label showing total customer count
   - Legend below chart with counts and percentages

3. frontend/src/app/features/dashboard/segment-breakdown/segment-breakdown.component.ts
   - Stacked horizontal bar chart per segment (enterprise / mid-market / smb)
   - Same green/yellow/red color scheme
   - Show count labels on each bar segment

4. frontend/src/app/features/dashboard/top-at-risk/top-at-risk.component.ts
   - Table of top 10 customers sorted by overall_score DESC
   - Columns: HeatBadge, Customer name, Segment, Churn, Payment, Margin, Overall (ScoreBar)
   - Each row is clickable, navigates to /customers/:id
   - Empty state message if no customers

5. frontend/src/app/features/dashboard/risk-trend/risk-trend.component.ts
   - Line chart showing portfolio health over time (portfolio_snapshots history)
   - Three lines: avg_churn_score, avg_payment_score, avg_margin_score
   - X-axis: snapshot created_at dates
   - Empty state if only one snapshot exists

SHARED COMPONENTS (implement fully, not stubs):

6. shared/components/heat-badge/heat-badge.component.ts
   - Input: heatLevel: 'green' | 'yellow' | 'red'
   - Renders colored pill with label (Green / Watch / At Risk)
   - Colors: green=#22c55e bg, yellow=#f59e0b bg, red=#ef4444 bg, white text

7. shared/components/score-bar/score-bar.component.ts
   - Input: score: number (0-100)
   - Horizontal bar, width = score%, color transitions green->yellow->red by score
   - Show numeric score label to the right

8. shared/components/loading-skeleton/loading-skeleton.component.ts
   - Input: type: 'card' | 'table' | 'chart' | 'text'
   - Renders shimmer placeholder matching the shape of the real component

DESIGN REQUIREMENTS:
- Use the heat colors (green/amber/red) as the primary visual language
- Dark card surfaces on a neutral background (not plain white)
- Intentional typography hierarchy: large portfolio health number, smaller labels
- Hover states on table rows and chart segments
- No generic Tailwind/Bootstrap template look

Reference: documentation/plan.md (Frontend section), documentation/plan/architecture.md
