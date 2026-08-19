import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-register', imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="auth-card"><h1>Creează un cont</h1><form [formGroup]="form" (ngSubmit)="submit()">
    <p class="auth-intro">Creezi un cont CompaSpot. Poți începe o cerere imediat; firma se adaugă doar când vrei să răspunzi la oportunități.</p>
    <label>E-mail<input type="email" formControlName="email" autocomplete="email" [readonly]="submitting() || registrationSucceeded()"><small>Folosește o adresă de e-mail pe care o poți reține.</small></label>
    <label>Parolă<input [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password" [readonly]="submitting() || registrationSucceeded()"><small>Folosește cel puțin 8 caractere. Nu sunt obligatorii simboluri sau litere mari.</small></label>
    <label class="checkbox-label"><input type="checkbox" [disabled]="submitting() || registrationSucceeded()" [checked]="showPassword()" (change)="showPassword.set(!showPassword())">Arată parola</label>
    @if (message()) { <p [class.error]="hasError()" [class.success]="registrationSucceeded()" role="alert">{{ message() }}</p> }
    @if (registrationSucceeded()) {
      <a class="button-link" routerLink="/sign-in" [queryParams]="returnUrl?{returnUrl}:null">Autentifică-te</a>
    } @else {
      <button type="submit" [disabled]="form.invalid || submitting()">{{ submitting() ? 'Se creează contul…' : 'Creează cont' }}</button>
    }
  </form>@if (!registrationSucceeded()) { <p>Ai deja cont? <a routerLink="/sign-in">Autentifică-te</a></p> }</section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterComponent {
  private readonly auth = inject(AuthService); readonly message = signal(''); readonly hasError = signal(false); readonly showPassword = signal(false);
  private readonly route=inject(ActivatedRoute);
  readonly returnUrl=this.safeReturnUrl(this.route.snapshot.queryParamMap.get('supplierInvite'));
  readonly submitting = signal(false); readonly registrationSucceeded = signal(false);
  readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }), password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }) });
  submit(): void {
    if (this.submitting() || this.registrationSucceeded()) return;
    if (this.form.invalid) { this.hasError.set(true); this.message.set('Introdu un e-mail valid și o parolă de cel puțin 8 caractere.'); return; }
    this.submitting.set(true); this.message.set(''); this.hasError.set(false);
    const request = this.auth.register(this.form.controls.email.value, this.form.controls.password.value);
    request.subscribe({
      next: () => {
        this.submitting.set(false);
        this.registrationSucceeded.set(true);
        this.form.controls.password.reset();
        this.hasError.set(false);
        this.message.set('Contul a fost creat. Autentificați-vă pentru a continua.');
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        this.hasError.set(true);
        this.message.set(this.registrationError(response));
      }
    });
  }

  private registrationError(response: HttpErrorResponse): string {
    if (typeof response.error === 'string' &&
        (response.error.trimStart().startsWith('<!DOCTYPE html') ||
         response.error.trimStart().startsWith('<html')))
      return 'Contul nu poate fi creat momentan. Încearcă din nou peste câteva minute.';

    const problem = this.problemDetails(response.error);
    const validationMessages = problem?.errors
      ? Object.values(problem.errors).flat().filter(message => message.trim().length > 0)
      : [];

    if (validationMessages.length > 0) return [...new Set(validationMessages)].join(' ');
    if (problem?.detail) return problem.detail;
    if (response.status === 0)
      return 'Nu ne putem conecta momentan. Verifică conexiunea și încearcă din nou.';
    if (response.status === 409)
      return 'Există deja un cont pentru această adresă de e-mail. Autentifică-te pentru a continua.';
    if (response.status === 429)
      return 'Au fost prea multe încercări. Așteptați un minut și încercați din nou.';
    if (response.status === 400)
      return problem?.title ?? 'Datele contului sunt invalide. Verificați e-mailul și cerințele parolei.';
    if (response.status >= 500)
      return 'Contul nu a putut fi creat momentan. Încearcă din nou peste câteva minute.';
    return 'Înregistrarea nu a putut fi finalizată. Încearcă din nou.';
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
  private safeReturnUrl(value:string|null):string|null{return value?.startsWith('/supplier-invitations/')?value:null;}
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}
