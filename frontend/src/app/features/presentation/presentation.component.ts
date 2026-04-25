import { Component, HostListener, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

interface Slide {
  id: number;
  label: string;
}

@Component({
  selector: 'app-presentation',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="slideshow" [attr.data-slide]="current()">

      <!-- Navigation -->
      <div class="nav-bar">
        <button class="nav-btn" (click)="prev()" [disabled]="current() === 0">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M15 18l-6-6 6-6"/>
          </svg>
        </button>

        <div class="slide-dots">
          @for (s of slides; track s.id) {
            <button class="dot" [class.active]="current() === s.id" (click)="goTo(s.id)" [title]="s.label"></button>
          }
        </div>

        <button class="nav-btn" (click)="next()" [disabled]="current() === slides.length - 1">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M9 18l6-6-6-6"/>
          </svg>
        </button>

        <span class="slide-counter">{{ current() + 1 }} / {{ slides.length }}</span>
      </div>

      <!-- Slides Container -->
      <div class="slides-viewport">

        <!-- SLIDE 1: Title & Problem Statement -->
        <div class="slide slide-1" [class.active]="current() === 0" [class.prev]="current() > 0">
          <div class="slide-content centered">
            <div class="badge">Hackathon April 2026</div>
            <h1 class="title-main">
              <span class="gradient-text">Customer Portfolio</span>
              <br>Thermometer
            </h1>
            <div class="title-divider"></div>
            <div class="problem-block">
              <h2 class="problem-title">The Problem</h2>
              <p class="problem-text">
                Energy companies store vast amounts of customer data in CRM systems — contracts,
                billing history, complaints, payment behavior — but this data is <strong>rarely
                translated into actionable intelligence</strong>.
              </p>
              <div class="problem-bullets">
                <div class="bullet">
                  <span class="bullet-icon red">⚡</span>
                  <span>Churn risk buried in fragmented CRM records</span>
                </div>
                <div class="bullet">
                  <span class="bullet-icon yellow">💳</span>
                  <span>Payment risk signals go unnoticed until it's too late</span>
                </div>
                <div class="bullet">
                  <span class="bullet-icon orange">📉</span>
                  <span>Margin erosion invisible until contracts are reviewed</span>
                </div>
                <div class="bullet">
                  <span class="bullet-icon purple">🔍</span>
                  <span>Organizations stay <em>reactive</em> instead of <em>proactive</em></span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- SLIDE 2: Platform Description & Approach -->
        <div class="slide slide-2" [class.active]="current() === 1" [class.prev]="current() > 1" [class.next]="current() < 1">
          <div class="slide-content centered">
            <div class="badge">Our Solution</div>
            <h1 class="title-main">
              <span class="gradient-text">Smart Analytics Layer</span>
              <br>on Top of Your CRM
            </h1>
            <div class="approach-cards">
              <div class="approach-card">
                <div class="approach-icon" style="background: rgba(99,102,241,0.15); color: #6366f1">🔒</div>
                <h3>Non-Intrusive</h3>
                <p>Read-only connection to existing CRM systems. Zero risk to core infrastructure. No data mutations.</p>
              </div>
              <div class="approach-card ai-card">
                <span class="ai-card-badge">✨ AI</span>
                <div class="approach-icon" style="background: rgba(99,102,241,0.15); color: #6366f1">🤖</div>
                <h3>AI-Powered</h3>
                <p>Large language models process structured & unstructured data — notes, emails, contracts — to surface hidden signals.</p>
              </div>
              <div class="approach-card">
                <div class="approach-icon" style="background: rgba(34,197,94,0.15); color: #22c55e">📊</div>
                <h3>Portfolio View</h3>
                <p>Dynamic heat overview of your entire customer base — green, yellow, red segments with clear reasoning.</p>
              </div>
              <div class="approach-card">
                <div class="approach-icon" style="background: rgba(249,115,22,0.15); color: #f97316">⚡</div>
                <h3>Actionable</h3>
                <p>Each risk flag comes with a suggested action: outreach, contract adjustment, advisory offer, or monitoring.</p>
              </div>
            </div>
          </div>
        </div>

        <!-- SLIDE 3: Platform Capabilities -->
        <div class="slide slide-3" [class.active]="current() === 2" [class.prev]="current() > 2" [class.next]="current() < 2">
          <div class="slide-content centered">
            <div class="badge">Capabilities</div>
            <h1 class="title-main">
              What the Platform <span class="gradient-text">Delivers</span>
            </h1>
            <div class="capabilities-grid">
              <div class="cap-item">
                <div class="cap-icon">🌡️</div>
                <div class="cap-title-row"><h3>Portfolio Heat Overview</h3></div>
                <p>Live green / yellow / red segmentation of your entire customer book with trend tracking over time.</p>
              </div>
              <div class="cap-item ai-cap">
                <div class="cap-icon">🔥</div>
                <div class="cap-title-row">
                  <h3>Risk Group Detection</h3>
                  <span class="ai-badge">✨ AI</span>
                </div>
                <p>Identifies churn risk, payment risk, margin behavior, service dissatisfaction — with full explainability.</p>
              </div>
              <div class="cap-item">
                <div class="cap-icon">👤</div>
                <div class="cap-title-row"><h3>Customer Deep-Dive</h3></div>
                <p>Per-customer risk breakdown, timeline of events, consumption patterns and contract analysis.</p>
              </div>
              <div class="cap-item ai-cap">
                <div class="cap-icon">💡</div>
                <div class="cap-title-row">
                  <h3>Suggested Actions</h3>
                  <span class="ai-badge">✨ AI</span>
                </div>
                <p>AI-generated, context-aware recommendations — transparent reasoning so advisors trust the output.</p>
              </div>
              <div class="cap-item ai-cap">
                <div class="cap-icon">⚡</div>
                <div class="cap-title-row">
                  <h3>Energy Meter Intelligence</h3>
                  <span class="ai-badge">✨ AI</span>
                </div>
                <p>Meter read anomaly detection and consumption trend analysis to catch imbalance early.</p>
              </div>
              <div class="cap-item">
                <div class="cap-icon">🗄️</div>
                <div class="cap-title-row"><h3>Unified Data Model</h3></div>
                <p>Normalises data from multiple CRM sources into a single enriched model for consistent analytics.</p>
              </div>
            </div>
          </div>
        </div>

        <!-- SLIDE 4: Integrations Diagram -->
        <div class="slide slide-4" [class.active]="current() === 3" [class.prev]="current() > 3" [class.next]="current() < 3">
          <div class="slide-content centered">
            <div class="badge">Architecture</div>
            <h1 class="title-main">
              Connecting <span class="gradient-text">Any CRM</span>
            </h1>
            <div class="diagram-wrapper">
              <svg class="arch-diagram" viewBox="0 0 900 420" fill="none" xmlns="http://www.w3.org/2000/svg">
                <!-- Defs -->
                <defs>
                  <linearGradient id="aiGrad" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stop-color="#6366f1"/>
                    <stop offset="100%" stop-color="#06b6d4"/>
                  </linearGradient>
                  <linearGradient id="outGrad" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stop-color="#22c55e"/>
                    <stop offset="100%" stop-color="#06b6d4"/>
                  </linearGradient>
                  <filter id="glow">
                    <feGaussianBlur stdDeviation="4" result="coloredBlur"/>
                    <feMerge><feMergeNode in="coloredBlur"/><feMergeNode in="SourceGraphic"/></feMerge>
                  </filter>
                  <marker id="arrow" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
                    <path d="M0,0 L0,6 L8,3 z" fill="#6366f1"/>
                  </marker>
                  <marker id="arrow-out" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
                    <path d="M0,0 L0,6 L8,3 z" fill="#22c55e"/>
                  </marker>
                </defs>

                <!-- CRM Source boxes (left column) -->
                <!-- DVEP CRM -->
                <rect x="20" y="30" width="150" height="56" rx="10" class="diag-box" stroke="#6366f1" stroke-width="1.5"/>
                <text x="95" y="54" text-anchor="middle" class="diag-accent-indigo" font-size="11" font-weight="600">DVEP CRM</text>
                <text x="95" y="72" text-anchor="middle" class="diag-text-muted" font-size="10">Customer &amp; Contract Data</text>

                <!-- SAP CRM -->
                <rect x="20" y="110" width="150" height="56" rx="10" class="diag-box" stroke="#f97316" stroke-width="1.5"/>
                <text x="95" y="134" text-anchor="middle" class="diag-accent-orange" font-size="11" font-weight="600">SAP CRM</text>
                <text x="95" y="152" text-anchor="middle" class="diag-text-muted" font-size="10">ERP &amp; Billing</text>

                <!-- Salesforce -->
                <rect x="20" y="190" width="150" height="56" rx="10" class="diag-box" stroke="#06b6d4" stroke-width="1.5"/>
                <text x="95" y="214" text-anchor="middle" class="diag-accent-cyan" font-size="11" font-weight="600">Salesforce</text>
                <text x="95" y="232" text-anchor="middle" class="diag-text-muted" font-size="10">Sales &amp; Interaction Logs</text>

                <!-- Microsoft Dynamics -->
                <rect x="20" y="270" width="150" height="56" rx="10" class="diag-box" stroke="#a855f7" stroke-width="1.5"/>
                <text x="95" y="294" text-anchor="middle" class="diag-accent-purple" font-size="11" font-weight="600">MS Dynamics</text>
                <text x="95" y="312" text-anchor="middle" class="diag-text-muted" font-size="10">Service &amp; Support</text>

                <!-- Custom CRM / Other -->
                <rect x="20" y="350" width="150" height="56" rx="10" class="diag-box" stroke="#64748b" stroke-width="1.5" stroke-dasharray="5,3"/>
                <text x="95" y="374" text-anchor="middle" class="diag-text-muted" font-size="11" font-weight="600">Custom / Other</text>
                <text x="95" y="392" text-anchor="middle" class="diag-text-muted" font-size="10">REST / SOAP / CSV</text>

                <!-- Connector lines from CRMs to AI box -->
                <line x1="170" y1="58" x2="320" y2="200" stroke="#6366f1" stroke-width="1.5" stroke-opacity="0.6" marker-end="url(#arrow)" class="connector"/>
                <line x1="170" y1="138" x2="320" y2="205" stroke="#f97316" stroke-width="1.5" stroke-opacity="0.6" marker-end="url(#arrow)" class="connector"/>
                <line x1="170" y1="218" x2="320" y2="210" stroke="#06b6d4" stroke-width="1.5" stroke-opacity="0.6" marker-end="url(#arrow)" class="connector"/>
                <line x1="170" y1="298" x2="320" y2="215" stroke="#a855f7" stroke-width="1.5" stroke-opacity="0.6" marker-end="url(#arrow)" class="connector"/>
                <line x1="170" y1="378" x2="320" y2="220" stroke="#64748b" stroke-width="1.5" stroke-opacity="0.5" stroke-dasharray="4,3" marker-end="url(#arrow)" class="connector"/>

                <!-- "API" labels on connector lines -->
                <rect x="210" y="88" width="34" height="16" rx="4" class="diag-label-bg" stroke="#6366f1" stroke-width="1"/>
                <text x="227" y="100" text-anchor="middle" class="diag-accent-indigo" font-size="9" font-weight="600">API</text>

                <rect x="210" y="143" width="34" height="16" rx="4" class="diag-label-bg" stroke="#f97316" stroke-width="1"/>
                <text x="227" y="155" text-anchor="middle" class="diag-accent-orange" font-size="9" font-weight="600">API</text>

                <rect x="185" y="208" width="34" height="16" rx="4" class="diag-label-bg" stroke="#06b6d4" stroke-width="1"/>
                <text x="202" y="220" text-anchor="middle" class="diag-accent-cyan" font-size="9" font-weight="600">API</text>

                <rect x="210" y="265" width="34" height="16" rx="4" class="diag-label-bg" stroke="#a855f7" stroke-width="1"/>
                <text x="227" y="277" text-anchor="middle" class="diag-accent-purple" font-size="9" font-weight="600">API</text>

                <rect x="200" y="315" width="34" height="16" rx="4" class="diag-label-bg" stroke="#64748b" stroke-width="1"/>
                <text x="217" y="327" text-anchor="middle" class="diag-text-muted" font-size="9" font-weight="600">CSV</text>

                <!-- AI Processing Hub (center) -->
                <rect x="320" y="140" width="200" height="140" rx="16" fill="url(#aiGrad)" fill-opacity="0.12" stroke="url(#aiGrad)" stroke-width="2" filter="url(#glow)"/>
                <text x="420" y="186" text-anchor="middle" fill="#a5b4fc" font-size="28">🧠</text>
                <text x="420" y="216" text-anchor="middle" class="diag-text-primary" font-size="13" font-weight="700">AI Processing</text>
                <text x="420" y="234" text-anchor="middle" class="diag-text-muted" font-size="10">Unified Data Model</text>
                <text x="420" y="250" text-anchor="middle" class="diag-text-muted" font-size="10">LLM Risk Analysis</text>
                <text x="420" y="266" text-anchor="middle" class="diag-text-muted" font-size="10">Pattern Detection</text>

                <!-- Output lines from AI box to insights -->
                <line x1="520" y1="185" x2="640" y2="90" stroke="#22c55e" stroke-width="1.5" stroke-opacity="0.7" marker-end="url(#arrow-out)" class="connector"/>
                <line x1="520" y1="200" x2="640" y2="175" stroke="#f59e0b" stroke-width="1.5" stroke-opacity="0.7" marker-end="url(#arrow-out)" class="connector"/>
                <line x1="520" y1="210" x2="640" y2="260" stroke="#ef4444" stroke-width="1.5" stroke-opacity="0.7" marker-end="url(#arrow-out)" class="connector"/>
                <line x1="520" y1="225" x2="640" y2="345" stroke="#6366f1" stroke-width="1.5" stroke-opacity="0.7" marker-end="url(#arrow-out)" class="connector"/>

                <!-- Output boxes (right column) -->
                <!-- Portfolio Heatmap -->
                <rect x="640" y="55" width="180" height="56" rx="10" class="diag-box" stroke="#22c55e" stroke-width="1.5"/>
                <text x="730" y="79" text-anchor="middle" class="diag-accent-green" font-size="11" font-weight="600">Portfolio Heatmap</text>
                <text x="730" y="97" text-anchor="middle" class="diag-text-muted" font-size="10">Green / Yellow / Red</text>

                <!-- Risk Groups -->
                <rect x="640" y="145" width="180" height="56" rx="10" class="diag-box" stroke="#f59e0b" stroke-width="1.5"/>
                <text x="730" y="169" text-anchor="middle" class="diag-accent-amber" font-size="11" font-weight="600">Risk Groups</text>
                <text x="730" y="187" text-anchor="middle" class="diag-text-muted" font-size="10">Churn / Payment / Margin</text>

                <!-- Suggested Actions -->
                <rect x="640" y="235" width="180" height="56" rx="10" class="diag-box" stroke="#ef4444" stroke-width="1.5"/>
                <text x="730" y="259" text-anchor="middle" class="diag-accent-red" font-size="11" font-weight="600">Suggested Actions</text>
                <text x="730" y="277" text-anchor="middle" class="diag-text-muted" font-size="10">Outreach / Advisory / Alert</text>

                <!-- Customer Intelligence -->
                <rect x="640" y="320" width="180" height="56" rx="10" class="diag-box" stroke="#6366f1" stroke-width="1.5"/>
                <text x="730" y="344" text-anchor="middle" class="diag-accent-indigo" font-size="11" font-weight="600">Customer Intelligence</text>
                <text x="730" y="362" text-anchor="middle" class="diag-text-muted" font-size="10">Deep-Dive &amp; Timeline</text>

              </svg>
            </div>
          </div>
        </div>

        <!-- SLIDE 5: Demo Time -->
        <div class="slide slide-5" [class.active]="current() === 4" [class.prev]="current() > 4" [class.next]="current() < 4">
          <div class="slide-content centered demo-slide">
            <div class="demo-pulse">
              <div class="pulse-ring ring-1"></div>
              <div class="pulse-ring ring-2"></div>
              <div class="pulse-ring ring-3"></div>
              <div class="demo-icon">▶</div>
            </div>
            <h1 class="demo-title">Time for <span class="gradient-text">Demo</span></h1>
            <p class="demo-subtitle">Let's see the Customer Portfolio Thermometer in action</p>
            <div class="demo-features">
              <span class="demo-feature">📊 Portfolio Heatmap</span>
              <span class="demo-feature">🔥 Risk Groups</span>
              <span class="demo-feature">👤 Customer Deep-Dive</span>
              <span class="demo-feature">⚡ Suggested Actions</span>
            </div>
          </div>
        </div>

        <!-- SLIDE 6: Thank You / Team -->
        <div class="slide slide-6" [class.active]="current() === 5" [class.prev]="current() > 5" [class.next]="current() < 5">
          <div class="slide-content centered">
            <h1 class="title-main thank-title">
              Thank <span class="gradient-text">You!</span>
            </h1>
            <p class="team-tagline">Built with ❤️ (and a lot of bananas 🍌) by</p>
            <h2 class="team-name">CodeMonkeys</h2>
            <div class="team-members">
              <span class="team-member">🐒 Cristi</span>
              <span class="team-member">🐒 Iulian</span>
              <span class="team-member">🐒 Peti</span>
            </div>

            <div class="morph-container" (mouseenter)="showMonkeys = true" (mouseleave)="showMonkeys = false">
              <img src="assets/team.jpg" alt="The Team" class="morph-img team-img" [class.fade-out]="showMonkeys"/>
              <img src="assets/monkeys.jpg" alt="CodeMonkeys" class="morph-img monkey-img" [class.fade-in]="showMonkeys"/>
              <div class="morph-hint" [class.hidden]="showMonkeys">
                <span>🐒 Hover to reveal our true nature</span>
              </div>
            </div>
          </div>
        </div>

      </div><!-- end slides-viewport -->
    </div><!-- end slideshow -->
  `,
  styles: [`
    :host {
      display: block;
      margin: -32px;
      height: 100vh;
    }

    .slideshow {
      width: 100%;
      height: 100vh;
      display: flex;
      flex-direction: column;
      position: relative;
      overflow: hidden;
      background: var(--color-bg);
    }

    /* NAV BAR */
    .nav-bar {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 20px;
      padding: 14px 32px;
      background: var(--color-surface);
      border-bottom: 1px solid var(--color-border);
      flex-shrink: 0;
      z-index: 10;
    }

    .nav-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 42px;
      height: 42px;
      border-radius: 10px;
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text-muted);
      cursor: pointer;
      transition: all 150ms;

      &:hover:not(:disabled) {
        background: var(--color-border);
        color: var(--color-text);
      }

      &:disabled {
        opacity: 0.35;
        cursor: not-allowed;
      }
    }

    .slide-dots {
      display: flex;
      gap: 10px;
      align-items: center;
    }

    .dot {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      border: none;
      background: var(--color-border);
      cursor: pointer;
      transition: all 200ms;

      &.active {
        background: #6366f1;
        transform: scale(1.35);
        box-shadow: 0 0 10px rgba(99,102,241,0.6);
      }

      &:hover:not(.active) {
        background: var(--color-text-muted);
      }
    }

    .slide-counter {
      font-size: 13px;
      color: var(--color-text-muted);
      font-variant-numeric: tabular-nums;
      min-width: 44px;
    }

    /* SLIDES */
    .slides-viewport {
      flex: 1;
      position: relative;
      overflow: hidden;
    }

    .slide {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 32px 48px;
      opacity: 0;
      transform: translateX(60px);
      transition: opacity 400ms ease, transform 400ms ease;
      pointer-events: none;

      &.active {
        opacity: 1;
        transform: translateX(0);
        pointer-events: all;
      }

      &.prev {
        transform: translateX(-60px);
      }

      &.next {
        transform: translateX(60px);
      }
    }

    .slide-content {
      width: 100%;
      max-width: 1200px;

      &.centered {
        display: flex;
        flex-direction: column;
        align-items: center;
        text-align: center;
      }
    }

    /* COMMON ELEMENTS */
    .badge {
      display: inline-block;
      padding: 6px 18px;
      border-radius: 999px;
      background: rgba(99,102,241,0.12);
      border: 1px solid rgba(99,102,241,0.3);
      color: #a5b4fc;
      font-size: 13px;
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      margin-bottom: 24px;
    }

    .title-main {
      font-size: clamp(32px, 4.5vw, 60px);
      font-weight: 800;
      line-height: 1.12;
      letter-spacing: -0.025em;
      color: var(--color-text);
      margin-bottom: 28px;
    }

    .gradient-text {
      background: linear-gradient(135deg, #6366f1 0%, #06b6d4 60%, #22c55e 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .title-divider {
      width: 72px;
      height: 4px;
      border-radius: 4px;
      background: linear-gradient(90deg, #6366f1, #06b6d4);
      margin: 0 auto 32px;
    }

    /* SLIDE 1 */
    .problem-block {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 20px;
      padding: 36px 48px;
      max-width: 820px;
      text-align: left;
    }

    .problem-title {
      font-size: 20px;
      font-weight: 700;
      color: var(--color-text);
      margin-bottom: 14px;
      text-align: center;
    }

    .problem-text {
      font-size: 16px;
      line-height: 1.7;
      color: var(--color-text-muted);
      margin-bottom: 24px;
      text-align: center;

      strong { color: var(--color-text); }
    }

    .problem-bullets {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }

    .bullet {
      display: flex;
      align-items: flex-start;
      gap: 14px;
      font-size: 15px;
      color: var(--color-text-muted);
      line-height: 1.5;

      em { color: var(--color-text); font-style: normal; font-weight: 600; }
    }

    .bullet-icon {
      font-size: 20px;
      flex-shrink: 0;
      margin-top: 1px;
    }

    /* SLIDE 2 */
    .approach-cards {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 20px;
      width: 100%;
      max-width: 960px;
    }

    .approach-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 18px;
      padding: 28px;
      text-align: left;
      transition: border-color 200ms, transform 200ms;
      position: relative;

      &:hover {
        border-color: rgba(99,102,241,0.4);
        transform: translateY(-3px);
      }

      h3 {
        font-size: 18px;
        font-weight: 700;
        color: var(--color-text);
        margin: 14px 0 8px;
      }

      p {
        font-size: 14px;
        line-height: 1.65;
        color: var(--color-text-muted);
      }

      &.ai-card {
        border-color: rgba(99,102,241,0.5);
        background: linear-gradient(135deg, rgba(99,102,241,0.07) 0%, rgba(6,182,212,0.07) 100%);
        box-shadow: 0 0 32px rgba(99,102,241,0.15), inset 0 0 24px rgba(99,102,241,0.04);

        &:hover {
          border-color: rgba(99,102,241,0.8);
          box-shadow: 0 0 48px rgba(99,102,241,0.25), inset 0 0 24px rgba(99,102,241,0.06);
          transform: translateY(-4px);
        }

        h3 { color: #a5b4fc; }
      }
    }

    .approach-icon {
      width: 56px;
      height: 56px;
      border-radius: 14px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 26px;
    }

    .ai-card-badge {
      position: absolute;
      top: 16px;
      right: 16px;
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 3px 10px;
      border-radius: 999px;
      background: rgba(99,102,241,0.15);
      border: 1px solid rgba(99,102,241,0.4);
      font-size: 11px;
      font-weight: 700;
      color: #a5b4fc;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }

    /* SLIDE 3 */
    .capabilities-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 18px;
      width: 100%;
      max-width: 1100px;
    }

    .cap-item {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 24px;
      text-align: left;
      transition: border-color 200ms, transform 200ms, box-shadow 200ms;
      position: relative;

      &:hover { border-color: rgba(99,102,241,0.4); transform: translateY(-2px); }

      &.ai-cap {
        border-color: rgba(99,102,241,0.35);
        background: linear-gradient(135deg, rgba(99,102,241,0.06) 0%, rgba(6,182,212,0.04) 100%);

        &:hover {
          border-color: rgba(99,102,241,0.65);
          box-shadow: 0 0 24px rgba(99,102,241,0.15);
        }
      }

      p {
        font-size: 13px;
        line-height: 1.65;
        color: var(--color-text-muted);
        margin-top: 6px;
      }
    }

    .cap-icon {
      font-size: 32px;
      margin-bottom: 12px;
      line-height: 1;
    }

    .cap-title-row {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 4px;

      h3 {
        font-size: 15px;
        font-weight: 700;
        color: var(--color-text);
      }
    }

    .ai-badge {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      padding: 2px 8px;
      border-radius: 999px;
      background: rgba(99,102,241,0.15);
      border: 1px solid rgba(99,102,241,0.35);
      font-size: 10px;
      font-weight: 700;
      color: #a5b4fc;
      letter-spacing: 0.05em;
      flex-shrink: 0;
    }

    /* SLIDE 4 */
    .diagram-wrapper {
      width: 100%;
      max-width: 940px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 16px;
      margin-top: 8px;
    }

    /* SVG diagram theme-aware classes */
    .arch-diagram {
      width: 100%;
      height: auto;

      .diag-box          { fill: var(--color-surface-2); }
      .diag-label-bg     { fill: var(--color-surface); }
      .diag-text-primary { fill: var(--color-text); }
      .diag-text-muted   { fill: var(--color-text-muted); }

      .diag-accent-indigo { fill: #6366f1; }
      .diag-accent-orange { fill: #f97316; }
      .diag-accent-cyan   { fill: #06b6d4; }
      .diag-accent-purple { fill: #a855f7; }
      .diag-accent-green  { fill: #22c55e; }
      .diag-accent-amber  { fill: #d97706; }
      .diag-accent-red    { fill: #ef4444; }

      .connector {
        stroke-dasharray: 300;
        stroke-dashoffset: 300;
        animation: drawLine 1.2s ease forwards;
      }

      @for $i from 1 through 9 {
        .connector:nth-child(#{$i + 10}) {
          animation-delay: #{($i - 1) * 0.12}s;
        }
      }
    }

    @keyframes drawLine {
      to { stroke-dashoffset: 0; }
    }

    /* SLIDE 5 */
    .demo-slide {
      gap: 28px;
    }

    .demo-pulse {
      position: relative;
      width: 120px;
      height: 120px;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 8px;
    }

    .pulse-ring {
      position: absolute;
      border-radius: 50%;
      border: 2px solid #6366f1;
      animation: pulseRing 2s ease-out infinite;

      &.ring-1 { width: 100%; height: 100%; animation-delay: 0s; }
      &.ring-2 { width: 80%; height: 80%; animation-delay: 0.4s; }
      &.ring-3 { width: 60%; height: 60%; animation-delay: 0.8s; }
    }

    @keyframes pulseRing {
      0%   { transform: scale(0.8); opacity: 0.8; }
      100% { transform: scale(1.6); opacity: 0; }
    }

    .demo-icon {
      font-size: 40px;
      color: #6366f1;
      filter: drop-shadow(0 0 14px rgba(99,102,241,0.8));
      z-index: 1;
    }

    .demo-title {
      font-size: clamp(44px, 7vw, 88px);
      font-weight: 900;
      letter-spacing: -0.03em;
      color: var(--color-text);
    }

    .demo-subtitle {
      font-size: 18px;
      color: var(--color-text-muted);
      max-width: 560px;
    }

    .demo-features {
      display: flex;
      gap: 14px;
      flex-wrap: wrap;
      justify-content: center;
    }

    .demo-feature {
      padding: 10px 20px;
      border-radius: 999px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      font-size: 15px;
      color: var(--color-text-muted);
    }

    .team-members {
      display: flex;
      gap: 20px;
      margin-bottom: 28px;
    }

    .team-member {
      padding: 10px 28px;
      border-radius: 999px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      font-size: 18px;
      font-weight: 600;
      color: var(--color-text);
    }

    /* SLIDE 6 */
    .thank-title {
      font-size: clamp(40px, 6vw, 76px);
      margin-bottom: 10px;
    }

    .team-tagline {
      font-size: 18px;
      color: var(--color-text-muted);
      margin-bottom: 6px;
    }

    .team-name {
      font-size: clamp(34px, 5vw, 60px);
      font-weight: 900;
      background: linear-gradient(135deg, #f97316, #ef4444, #a855f7);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      letter-spacing: -0.02em;
      margin-bottom: 20px;
    }

    .morph-container {
      position: relative;
      width: 520px;
      max-width: 100%;
      height: 360px;
      border-radius: 24px;
      overflow: hidden;
      cursor: pointer;
      box-shadow: 0 0 48px rgba(99,102,241,0.25), 0 8px 40px rgba(0,0,0,0.4);
      border: 2px solid var(--color-border);
      transition: border-color 300ms;

      &:hover {
        border-color: #6366f1;
        box-shadow: 0 0 72px rgba(99,102,241,0.4), 0 8px 40px rgba(0,0,0,0.5);
      }
    }

    .morph-img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: opacity 600ms ease;
    }

    .team-img {
      opacity: 1;
      z-index: 1;

      &.fade-out {
        opacity: 0;
      }
    }

    .monkey-img {
      opacity: 0;
      z-index: 2;

      &.fade-in {
        opacity: 1;
      }
    }

    .morph-hint {
      position: absolute;
      bottom: 12px;
      left: 50%;
      transform: translateX(-50%);
      z-index: 3;
      background: rgba(0,0,0,0.65);
      backdrop-filter: blur(4px);
      padding: 6px 14px;
      border-radius: 999px;
      font-size: 12px;
      color: #e2e8f0;
      white-space: nowrap;
      transition: opacity 300ms;

      &.hidden {
        opacity: 0;
        pointer-events: none;
      }
    }
  `],
})
export class PresentationComponent {
  slides: Slide[] = [
    { id: 0, label: 'Title & Problem' },
    { id: 1, label: 'Platform Description' },
    { id: 2, label: 'Capabilities' },
    { id: 3, label: 'Integrations' },
    { id: 4, label: 'Demo Time' },
    { id: 5, label: 'Thank You' },
  ];

  current = signal(0);
  showMonkeys = false;

  next(): void {
    if (this.current() < this.slides.length - 1) {
      this.current.update(v => v + 1);
    }
  }

  prev(): void {
    if (this.current() > 0) {
      this.current.update(v => v - 1);
    }
  }

  goTo(index: number): void {
    this.current.set(index);
  }

  @HostListener('window:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
      this.next();
    } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
      this.prev();
    }
  }
}
