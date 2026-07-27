import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { AccountSwitcherComponent } from './account-switcher.component';

describe('AccountSwitcherComponent', () => {
  it('offers and activates the missing Provider profile', () => {
    const profiles = [
      { userId: 'user-id', email: 'buyer@example.test', accountType: 'Buyer' as const }
    ];
    const auth = {
      profiles: () => profiles,
      session: () => profiles[0],
      switchProfile: vi.fn(),
      addProfile: vi.fn().mockReturnValue(of({ accessToken: 'token', expiresAt: '' }))
    };
    TestBed.configureTestingModule({
      imports: [AccountSwitcherComponent],
      providers: [{ provide: AuthService, useValue: auth }]
    });

    const fixture = TestBed.createComponent(AccountSwitcherComponent);
    const emitted: string[] = [];
    fixture.componentInstance.profileAdded.subscribe(value => emitted.push(value));
    fixture.detectChanges();
    const addButton = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button')
    ).find(button => button.textContent?.includes('Furnizor'));

    addButton?.click();

    expect(auth.addProfile).toHaveBeenCalledWith('Supplier');
    expect(emitted).toEqual(['Supplier']);
  });

  it('keeps the missing profile action available after an API error', () => {
    const profiles = [
      { userId: 'user-id', email: 'supplier@example.test', accountType: 'Supplier' as const }
    ];
    const auth = {
      profiles: () => profiles,
      session: () => profiles[0],
      switchProfile: vi.fn(),
      addProfile: vi.fn().mockReturnValue(throwError(() => new Error('offline')))
    };
    TestBed.configureTestingModule({
      imports: [AccountSwitcherComponent],
      providers: [{ provide: AuthService, useValue: auth }]
    });

    const fixture = TestBed.createComponent(AccountSwitcherComponent);
    fixture.detectChanges();
    const addButton = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button')
    ).find(button => button.textContent?.includes('Cumpărător'));
    addButton?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.addingProfile()).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent)
      .toContain('Profilul nu a putut fi adăugat');
  });
});
