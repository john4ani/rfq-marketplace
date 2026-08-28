import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { FeaturesService } from '../../core/features/features.service';
import { SeoLandingComponent } from './seo-landing.component';

describe('SeoLandingComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeoLandingComponent],
      providers: [provideHttpClient(), provideRouter([])]
    }).compileComponents();
  });

  it('renders the SEAP SEO content instead of the live market when the marketplace flag is off', () => {
    const features = TestBed.inject(FeaturesService);
    features.setMarketplaceEnabledForTest(false);

    const fixture = TestBed.createComponent(SeoLandingComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Redactare ofertă tehnică SEAP');
    expect(text).toContain('Model propunere tehnică construcții');
    expect(text).toContain('Soft generare deviz caiet de sarcini');
    expect(text).toContain('Consultanță achiziții publice AI');
    expect(text).toContain('Termenii și Condițiile');
    expect((fixture.nativeElement as HTMLElement).querySelector('app-live-market')).toBeNull();
  });

  it('delegates to the live marketplace when the flag is on', async () => {
    const features = TestBed.inject(FeaturesService);
    features.setMarketplaceEnabledForTest(true);

    const fixture = TestBed.createComponent(SeoLandingComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-live-market')).not.toBeNull();
  });
});
