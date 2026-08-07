import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-sign-in', imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="auth-card"><h1>Autentificare</h1><form [formGroup]="form" (ngSubmit)="submit()">
    <label>E-mail<input type="email" formControlName="email" autocomplete="email"><small>Introduceți e-mailul folosit la înregistrare.</small></label>
    <label>Parolă<input [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password"><small>Parola conține cel puțin 8 caractere.</small></label>
    <label class="checkbox-label"><input type="checkbox" [checked]="showPassword()" (change)="showPassword.set(!showPassword())">Arată parola</label>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    <button type="submit" [disabled]="form.invalid || submitting()">Autentificare</button>
  </form><p><a routerLink="/forgot-password">Ați uitat parola?</a></p>
  <p>Nu aveți cont? <a routerLink="/register">Creați un cont</a></p></section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SignInComponent {
  private readonly auth = inject(AuthService); private readonly router = inject(Router); private readonly route=inject(ActivatedRoute);
  private readonly returnUrl=this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  readonly submitting = signal(false); readonly error = signal(''); readonly showPassword = signal(false);
  readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }), password: new FormControl('', { nonNullable: true, validators: Validators.required }) });
  submit(): void {
    if (this.form.invalid) { this.error.set('Introduceți un e-mail valid și parola.'); return; }
    this.submitting.set(true); this.error.set('');
    this.auth.login(this.form.controls.email.value, this.form.controls.password.value).subscribe({
      next: () => void this.router.navigateByUrl(this.returnUrl??'/dashboard'),
      error: (response) => {
        this.error.set(response.status === 0
          ? 'API-ul nu poate fi contactat. Verificați dacă Marketplace.Api rulează.'
          : 'E-mailul sau parola este incorectă.');
        this.submitting.set(false);
      }
    });
  }
  private safeReturnUrl(value:string|null):string|null{return value?.startsWith('/')&&!value.startsWith('//')?value:null;}
}
