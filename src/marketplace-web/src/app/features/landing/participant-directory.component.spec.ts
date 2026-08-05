import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LiveMarketService } from '../../core/marketplace/live-market.service';
import { ParticipantDirectoryComponent } from './participant-directory.component';

describe('ParticipantDirectoryComponent', () => {
  let fixture: ComponentFixture<ParticipantDirectoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantDirectoryComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session: () => ({ accountType: 'Buyer' }) } },
        { provide: LiveMarketService, useValue: { getSnapshot: () => of({
          generatedAt: new Date().toISOString(), isLocationFiltered: true, filterCity: 'Iași',
          filterCounty: 'Iași', approximateRadiusKm: 30,
          onlineUserCount: 3, onlineBuyerCount: 3, onlineSupplierCount: 1,
          participants: [
            { id: 'local-user', name: 'Cumpărător local', profileType: 'Buyer', isOnline: true, city: 'Iași', county: 'Iași', isSameCity: true },
            { id: 'local-user', name: 'Furnizor local', profileType: 'Supplier', isOnline: true, city: 'Iași', county: 'Iași', isSameCity: true },
            { id: 'offline-provider', name: 'Furnizor offline', profileType: 'Supplier', isOnline: false, city: 'Iași', county: 'Iași', isSameCity: true }
          ]
        }) } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(ParticipantDirectoryComponent);
    fixture.detectChanges();
  });

  afterEach(() => fixture.destroy());

  it('counts distinct directory accounts instead of the global online metric or role rows', () => {
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('1 participant');
    expect(text).toContain('1 online acum');
    expect(text).not.toContain('3 online acum');
    expect(text).not.toContain('2 participanți');
  });
});
