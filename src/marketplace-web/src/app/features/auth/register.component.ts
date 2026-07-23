import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-register', imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="auth-card"><h1>Creează un cont</h1><form [formGroup]="form" (ngSubmit)="submit()">
    <fieldset><legend>Cum vrei să folosești platforma?</legend><div class="role-options">
      <button type="button" [class.active]="selectedRoles().includes('Buyer')" [attr.aria-pressed]="selectedRoles().includes('Buyer')" (click)="toggleRole('Buyer')"><strong>Cumpărător</strong><span>Hai să economisim bani</span></button>
      <button type="button" [class.active]="selectedRoles().includes('Supplier')" [attr.aria-pressed]="selectedRoles().includes('Supplier')" (click)="toggleRole('Supplier')"><strong>Furnizor</strong><span>Hai să facem bani</span></button>
    </div><small>Poți selecta unul sau ambele profiluri.</small></fieldset>
    <label>E-mail<input type="email" formControlName="email" autocomplete="email"><small>Folosește o adresă de e-mail pe care o poți reține.</small></label>
    <label>Parolă<input [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password"><small>Folosește cel puțin 8 caractere. Nu sunt obligatorii simboluri sau litere mari.</small></label>
    <label class="checkbox-label"><input type="checkbox" [checked]="showPassword()" (change)="showPassword.set(!showPassword())">Arată parola</label>
    @if (message()) { <p [class.error]="hasError()" role="alert">{{ message() }}</p> }
    <button type="submit" [disabled]="form.invalid">Înregistrare</button>
  </form><a routerLink="/sign-in">Înapoi la autentificare</a></section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterComponent {
  private readonly auth = inject(AuthService); readonly message = signal(''); readonly hasError = signal(false); readonly showPassword = signal(false);
  readonly selectedRoles = signal<('Buyer'|'Supplier')[]>(['Buyer']);
  readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }), password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }) });
  toggleRole(role: 'Buyer'|'Supplier'): void {
    const roles = this.selectedRoles();
    this.selectedRoles.set(roles.includes(role) ? roles.filter(value => value !== role) : [...roles, role]);
  }
  submit(): void {
    if (this.form.invalid || this.selectedRoles().length === 0) { this.hasError.set(true); this.message.set('Selectați cel puțin un profil și introduceți un e-mail valid și o parolă de cel puțin 8 caractere.'); return; }
    this.message.set(''); this.hasError.set(false);
    const request = this.auth.register(this.form.controls.email.value, this.form.controls.password.value, this.selectedRoles());
    request.subscribe({
      next: () => { this.hasError.set(false); this.message.set('Contul a fost creat. Vă puteți autentifica acum.'); },
      error: (response: HttpErrorResponse) => { this.hasError.set(true); this.message.set(this.registrationError(response)); }
    });
  }

  private registrationError(response: HttpErrorResponse): string {
    if (typeof response.error === 'string' &&
        (response.error.trimStart().startsWith('<!DOCTYPE html') ||
         response.error.trimStart().startsWith('<html')))
      return 'Cererea a ajuns la serverul web, nu la API. Reporniți serverul Angular pentru a încărca configurația proxy.';

    const problem = this.problemDetails(response.error);
    const validationMessages = problem?.errors
      ? Object.values(problem.errors).flat().filter(message => message.trim().length > 0)
      : [];

    if (validationMessages.length > 0) return [...new Set(validationMessages)].join(' ');
    if (problem?.detail) return problem.detail;
    if (response.status === 0)
      return 'API-ul nu poate fi contactat. Porniți Marketplace.Api în Visual Studio și încercați din nou.';
    if (response.status === 409)
      return 'Există deja un cont de acest tip pentru adresa de e-mail. Autentificați-vă sau alegeți celălalt tip de cont.';
    if (response.status === 429)
      return 'Au fost prea multe încercări. Așteptați un minut și încercați din nou.';
    if (response.status === 400)
      return problem?.title ?? 'Datele contului sunt invalide. Verificați e-mailul și cerințele parolei.';
    if (response.status >= 500)
      return 'Serverul a întâmpinat o eroare la crearea contului. Verificați fereastra Marketplace.Api din Visual Studio.';
    return `Înregistrarea a eșuat cu statusul HTTP ${response.status}. Verificați răspunsul API și încercați din nou.`;
  }

  private problemDetails(value: unknown): ProblemDetails | null {
    if (typeof value === 'string') {
      if (value.trimStart().startsWith('<!DOCTYPE html') || value.trimStart().startsWith('<html'))
        return null;
      try {
        return JSON.parse(value) as ProblemDetails;
      } catch {
        return value.trim() ? { detail: value } : null;
      }
    }
    return value && typeof value === 'object' ? value as ProblemDetails : null;
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}
