import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LiveMarketService } from '../../core/marketplace/live-market.service';
import { OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { LiveMarketComponent } from './live-market.component';

describe('LiveMarketComponent', () => {
  let fixture: ComponentFixture<LiveMarketComponent>;

  beforeEach(async () => {
    const profiles = [
      { userId: 'user-id', email: 'dual@example.test', accountType: 'Buyer' as const },
      { userId: 'user-id', email: 'dual@example.test', accountType: 'Supplier' as const }
    ];
    await TestBed.configureTestingModule({
      imports: [LiveMarketComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: {
          isAuthenticated: () => true,
          profiles: () => profiles,
          session: () => ({ ...profiles[0], accountType: 'Buyer' as const })
        } },
        { provide: LiveMarketService, useValue: { getSnapshot: () => of({
          generatedAt: new Date().toISOString(), isLocationFiltered: true, filterCity: 'Iași',
          filterCounty: 'Iași', approximateRadiusKm: 30,
          onlineUserCount: 1, onlineBuyerCount: 1, onlineSupplierCount: 1,
          participants: [
            { id: 'user-id', name: 'Buyer', profileType: 'Buyer', isOnline: true, city: 'Iași', county: 'Iași', isSameCity: true },
            { id: 'user-id', name: 'Provider', profileType: 'Supplier', isOnline: true, city: 'Iași', county: 'Iași', isSameCity: true }
          ]
        }) } },
        { provide: OpenRfqService, useValue: { list: () => of([]) } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(LiveMarketComponent);
    fixture.detectChanges();
  });

  it('shows both roles and counts the signed-in identity online in each role', () => {
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Tu ești online ca');
    expect(text).toContain('Cumpărător și Furnizor');
    expect(text).toContain('1 online acum');
    expect(text).toContain('1 cumpărători online');
    expect(text).toContain('1 furnizori online');
  });
});
