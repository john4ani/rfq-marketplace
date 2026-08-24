import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { BackendAvailabilityService } from './core/connectivity/backend-availability.service';

@Component({ template: '' })
class SeapStubComponent {}

describe('App', () => {
  beforeEach(async () => {
    localStorage.setItem('compaspot.language', 'ro');
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([{ path: 'seap/tenders', component: SeapStubComponent }]), provideHttpClient()] }).compileComponents();
  });

  afterEach(() => localStorage.removeItem('compaspot.language'));
  it('creates the application shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelector('.brand')?.textContent).toContain('CompaSpot');
    const homeLink = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.public-header nav a[href="/"]');
    expect(homeLink?.textContent).toContain('Piața Live');
    expect(homeLink?.getAttribute('href')).toBe('/');
  });

  it('switches the shell to English via the language toggle', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const enButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.language-toggle button:nth-child(2)');
    expect(enButton).toBeTruthy();
    enButton!.click();
    fixture.detectChanges();
    const homeLink = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.public-header nav a[href="/"]');
    expect(homeLink?.textContent).toContain('Live Marketplace');
    expect(localStorage.getItem('compaspot.language')).toBe('en');
  });

  it('shows one actionable status when the backend is unavailable', () => {
    const backend = TestBed.inject(BackendAvailabilityService);
    backend.markUnavailable();

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.service-status');
    expect(status?.textContent).toContain('Serviciul este temporar indisponibil');
    expect(status?.querySelector('button')?.textContent).toContain('Reîncearcă');
  });

  it('opens and closes the complete mobile navigation without losing account actions', () => {
    const auth = TestBed.inject(AuthService);
    Object.defineProperties(auth, {
      isAuthenticated: { value: () => true },
      isAdministrator: { value: () => false },
      session: { value: () => ({ userId: 'user-1', email: 'buyer@example.com', accountType: 'Buyer', roles: ['Buyer'] }) },
      profiles: { value: () => [{ userId: 'user-1', email: 'buyer@example.com', accountType: 'Buyer' }] }
    });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const toggle = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.mobile-menu-toggle')!;
    toggle.click();
    fixture.detectChanges();

    const rail = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.side-rail');
    expect(rail?.classList.contains('mobile-open')).toBe(true);
    expect(rail?.textContent).toContain('Acasă');
    expect(rail?.textContent).toContain('Cererile mele');
    expect(rail?.textContent).toContain('Oferte');
    expect(rail?.textContent).toContain('Anunțuri SEAP');
    expect(rail?.textContent).toContain('Cont');
    expect(rail?.textContent).not.toContain('Piața Live');
    expect(rail?.textContent).not.toContain('Furnizori');
    expect(rail?.textContent).toContain('Notificări');
    expect(rail?.textContent).toContain('Deconectare');

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.navigation-scrim')!.click();
    fixture.detectChanges();
    expect(rail?.classList.contains('mobile-open')).toBe(false);
  });

  it('keeps the company rail with "Anunțuri SEAP" visible on SEAP pages', async () => {
    const auth = TestBed.inject(AuthService);
    Object.defineProperties(auth, {
      isAuthenticated: { value: () => true },
      isAdministrator: { value: () => false },
      session: { value: () => ({ userId: 'user-1', email: 'provider@example.com', accountType: 'Buyer', roles: ['Buyer'] }) },
      profiles: { value: () => [{ userId: 'user-1', email: 'provider@example.com', accountType: 'Buyer' }] }
    });
    await TestBed.inject(Router).navigateByUrl('/seap/tenders');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const rail = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.side-rail');
    expect(rail?.textContent).toContain('Firma ta');
    expect(rail?.textContent).toContain('Anunțuri SEAP');
    expect(rail?.textContent).not.toContain('Cererile mele');
  });
});
