import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ProfileService } from '../../core/profiles/profile.service';
import { RepresentedCompaniesComponent } from './represented-companies.component';

describe('RepresentedCompaniesComponent', () => {
  it('offers company registration instead of a dead end when no company is represented', () => {
    TestBed.configureTestingModule({
      imports: [RepresentedCompaniesComponent],
      providers: [provideRouter([]), { provide: ProfileService, useValue: { getMyCompanies: () => of([]) } }]
    });
    const fixture = TestBed.createComponent(RepresentedCompaniesComponent);
    fixture.detectChanges();

    const action = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a[href="/account/company/new"]');
    expect(action?.textContent).toContain('Adaugă firma');
  });

  it('explains that only one company per account is supported when a company already exists', () => {
    TestBed.configureTestingModule({
      imports: [RepresentedCompaniesComponent],
      providers: [provideRouter([]), { provide: ProfileService, useValue: { getMyCompanies: () => of([{ id: 'company-1', name: 'SC Ion Balan', role: 'Proprietar' as const }]) } }]
    });
    const fixture = TestBed.createComponent(RepresentedCompaniesComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('SC Ion Balan');
    expect(text).toContain('Momentan este suportată o singură firmă per cont');
    expect((fixture.nativeElement as HTMLElement).querySelector('a[href="/account/company/new"]')).toBeNull();
  });
});
