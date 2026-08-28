import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { FeaturesService } from './features.service';

describe('FeaturesService', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ providers: [provideHttpClient()] }).compileComponents();
  });

  it('defaults to marketplace enabled (fail-open) until the flag endpoint responds', () => {
    const service = TestBed.inject(FeaturesService);
    expect(service.marketplaceEnabled()).toBe(true);
  });

  it('keeps the marketplace enabled when the flag endpoint is unavailable', async () => {
    const service = TestBed.inject(FeaturesService);
    await service.initialize();
    expect(service.marketplaceEnabled()).toBe(true);
  });

  it('can override the flag through the test seam', () => {
    const service = TestBed.inject(FeaturesService);
    service.setMarketplaceEnabledForTest(false);
    expect(service.marketplaceEnabled()).toBe(false);
  });
});
