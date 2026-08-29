import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { SeapService, SeapTender } from '../../core/seap/seap.service';
import { SeapTenderDetailComponent } from './seap-tender-detail.component';

registerLocaleData(localeRo);

describe('SeapTenderDetailComponent', () => {
  const tender: SeapTender = {
    id: 't1',
    seapExternalId: 'SCN1178750',
    title: 'Reabilitare infrastructură rutieră în comuna Udești',
    description: null,
    cpvCodes: ['45233120-6'],
    cpvNames: ['Lucrari de constructii de drumuri'],
    county: 'Suceava',
    categoryId: null,
    category: null,
    estimatedValue: 4_949_355,
    currency: 'RON',
    buyerName: 'Comuna Udesti',
    publicationDate: '2026-08-14T00:00:00Z',
    deadline: '2026-09-04T15:00:00Z',
    status: 'Imported',
    proposalTitle: null,
    technicalProposalUrl: null,
    billOfQuantitiesUrl: null,
    attachments: []
  };
  const bidStatus = { status: 'Imported' as const, proposalTitle: null, technicalProposalUrl: null, billOfQuantitiesUrl: null };
  const pricing = { estimatedValueEur: 989_871, priceEur: 500, freeGenerationsRemaining: 1, shadowMode: true };

  function setup(options: { startAutoBid: ReturnType<typeof vi.fn> }) {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 't1' }) } } },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            startAutoBid: options.startAutoBid,
            downloadBidFile: vi.fn()
          }
        }
      ]
    });
    return TestBed.createComponent(SeapTenderDetailComponent);
  }

  function clickAutoBid(fixture: ReturnType<typeof setup>): void {
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button');
    expect(button).not.toBeNull();
    button!.click();
    fixture.detectChanges();
  }

  it('shows the backend ProblemDetails detail when auto-bid fails (instead of the generic message)', () => {
    const fixture = setup({
      startAutoBid: vi.fn().mockReturnValue(throwError(() => ({
        error: { detail: 'Serviciul Claude este temporar indisponibil (circuit deschis). Reîncearcă peste aproximativ o oră.' }
      })))
    });
    fixture.detectChanges();
    clickAutoBid(fixture);

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[role="status"]');
    expect(status?.textContent).toContain('Serviciul Claude este temporar indisponibil');
    expect(status?.textContent).not.toContain('Ofertarea automată nu a putut fi pornită');
  });

  it('falls back to the generic message when the failure carries no actionable detail', () => {
    const fixture = setup({ startAutoBid: vi.fn().mockReturnValue(throwError(() => ({ error: {} }))) });
    fixture.detectChanges();
    clickAutoBid(fixture);

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[role="status"]');
    expect(status?.textContent).toContain('Ofertarea automată nu a putut fi pornită. Încearcă din nou.');
  });
});
