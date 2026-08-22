import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { SupplierDiscoveryService, SupplierWebCandidate } from '../../core/supplier-discovery/supplier-discovery.service';
import { DiscoveryNeedReviewComponent } from './discovery-need-review.component';

describe('DiscoveryNeedReviewComponent', () => {
  const candidate: SupplierWebCandidate = {
    companyName: 'Firma găsită SRL', locality: 'Marginea', county: 'Suceava',
    website: null, phone: null, email: 'contact@firma.example', activityEvidence: 'CAEN 0220', caenCode: '0220',
    productEvidence: 'Lemn rotund', geographicRelevance: 'în rază', confidence: 'high',
    sourceUrls: ['https://example.test/firma'], sources: ['OpenAI Web Search'], whyRelevant: 'Activitate relevantă.', distanceKm: 2
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DiscoveryNeedReviewComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'need-1' } } } },
        { provide: SupplierDiscoveryService, useValue: {
          needReview: () => of({
            rfqId: 'need-1', title: 'Lemn rotund', deliveryLocality: 'Marginea', county: 'Suceava', quotationDeadline: '2026-09-04T13:14:00Z',
            request: { categoryName: 'Lemn și plăci', lines: [] }, buyer: {}, registeredCompanies: [], candidates: []
          })
        } }
      ]
    }).compileComponents();
  });

  it('prefills a review form before a discovered company is added to the registry', () => {
    const fixture = TestBed.createComponent(DiscoveryNeedReviewComponent);
    const component = fixture.componentInstance;

    component.startReview(candidate);

    expect(component.reviewingCompanyName()).toBe('Firma găsită SRL');
    expect(component.candidateForm.getRawValue()).toEqual({
      companyName: 'Firma găsită SRL', locality: 'Marginea', county: 'Suceava',
      email: 'contact@firma.example', phone: '', website: ''
    });
  });
});
