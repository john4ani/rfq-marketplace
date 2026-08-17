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
});
