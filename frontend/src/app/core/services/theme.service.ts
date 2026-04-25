import { computed, Injectable, signal } from '@angular/core';

type Theme = 'dark' | 'light';

const STORAGE_KEY = 'app-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>(this.loadTheme());

  readonly theme = this._theme.asReadonly();
  readonly isLight = computed(() => this._theme() === 'light');

  toggle(): void {
    const next: Theme = this._theme() === 'dark' ? 'light' : 'dark';
    this.apply(next);
  }

  private apply(theme: Theme): void {
    this._theme.set(theme);
    localStorage.setItem(STORAGE_KEY, theme);
    this.setDomTheme(theme);
  }

  private setDomTheme(theme: Theme): void {
    if (theme === 'light') {
      document.documentElement.setAttribute('data-theme', 'light');
    } else {
      document.documentElement.removeAttribute('data-theme');
    }
  }

  private loadTheme(): Theme {
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null;
    if (stored === 'light' || stored === 'dark') {
      this.setDomTheme(stored);
      return stored;
    }
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme: Theme = prefersDark ? 'dark' : 'light';
    this.setDomTheme(theme);
    return theme;
  }
}
