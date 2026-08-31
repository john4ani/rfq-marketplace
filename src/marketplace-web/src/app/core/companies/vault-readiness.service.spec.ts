import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CompanyVaultService } from './company-vault.service';
import { VaultReadinessService } from './vault-readiness.service';

describe('VaultReadinessService', () => {
  let service: VaultReadinessService;
  let list: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    list = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: CompanyVaultService, useValue: { list } }]
    });
    service = TestBed.inject(VaultReadinessService);
  });

  it('considers the repository usable only when a processed (extracted) document with chunks exists', () => {
    const doc = (extracted: boolean, chunkCount: number) => ({
      id: 'd1', fileName: 'a.pdf', documentType: 'PDF', extracted, pageCount: 2, chunkCount, uploadedAt: '2026-08-01T10:00:00Z'
    });

    let usable = false;
    list.mockReturnValue(of([]));
    service.hasUsableDocuments('c1').subscribe(value => (usable = value));
    expect(usable).toBe(false);

    // Pending document (still extracting) is not usable yet.
    list.mockReturnValue(of([doc(false, 0)]));
    service.hasUsableDocuments('c1').subscribe(value => (usable = value));
    expect(usable).toBe(false);

    // Extracted but not chunked is not usable yet.
    list.mockReturnValue(of([doc(true, 0)]));
    service.hasUsableDocuments('c1').subscribe(value => (usable = value));
    expect(usable).toBe(false);

    // Extracted and chunked is usable for auto-bidding.
    list.mockReturnValue(of([doc(true, 4)]));
    service.hasUsableDocuments('c1').subscribe(value => (usable = value));
    expect(usable).toBe(true);
  });

  it('persists the permanent dismissal per company', () => {
    localStorage.removeItem('compaspot.vaultPromptDismissed.c1');
    localStorage.removeItem('compaspot.vaultPromptDismissed.c2');

    expect(service.isPromptDismissed('c1')).toBe(false);
    service.dismissForever('c1');
    expect(service.isPromptDismissed('c1')).toBe(true);
    expect(service.isPromptDismissed('c2')).toBe(false);

    localStorage.removeItem('compaspot.vaultPromptDismissed.c1');
  });
});
