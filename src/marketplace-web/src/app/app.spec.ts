import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { BackendAvailabilityService } from './core/connectivity/backend-availability.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([]), provideHttpClient()] }).compileComponents();
  });
  it('creates the application shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelector('.brand')?.textContent).toContain('Piața Live');
    const homeLink = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.nav-link[href="/"]');
    expect(homeLink?.textContent).toContain('Acasă');
    expect(homeLink?.getAttribute('href')).toBe('/');
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
});
