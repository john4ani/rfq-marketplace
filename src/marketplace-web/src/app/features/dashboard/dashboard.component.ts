import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  template: `<section>
    <p class="eyebrow">Contul meu</p>
    <h1>Panoul marketplace</h1>
    <p>Profilul și funcționalitățile marketplace vor fi adăugate în etapele următoare.</p>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    <button type="button" (click)="signOut()" [disabled]="signingOut()">
      {{ signingOut() ? 'Deconectare…' : 'Deconectare' }}
    </button>
  </section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly signingOut = signal(false);
  readonly error = signal('');

  signOut(): void {
    this.signingOut.set(true);
    this.error.set('');
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/sign-in'),
      error: () => {
        this.signingOut.set(false);
        this.error.set('Sesiunea locală a fost închisă, dar API-ul nu a putut revoca sesiunea de pe server. Porniți API-ul și încercați din nou.');
      }
    });
  }
}
