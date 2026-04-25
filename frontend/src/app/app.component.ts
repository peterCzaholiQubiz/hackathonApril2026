import { Component, inject, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { StatusService } from './core/services/status.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly statusService = inject(StatusService);
  readonly themeService = inject(ThemeService);

  ngOnInit(): void {
    this.statusService.check().subscribe({
      error: () => undefined,
    });
  }
}
