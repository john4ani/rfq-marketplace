import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CompanyQualificationService } from '../../core/companies/company-qualification.service';
import { CompanyQualificationComponent } from './company-qualification.component';

describe('CompanyQualificationComponent', () => {
  const qualification = {
    legal: {
      legalRepresentativeName: 'Ion Popescu', legalRepresentativeRole: 'Administrator', legalForm: 'SRL',
      bankAccounts: [{ bankName: 'BCR', iban: 'RO49BCR123456789', currency: 'RON' }]
    },
    experience: [{
      id: 'exp-1', clientName: 'Primăria Udești', contractTitle: 'Reabilitare drum', contractNumberDate: '123/2024',
      valueAmount: 100000, valueCurrency: 'RON', cpvCode: '45233120-6', completionDate: '2024-05-01', documentRefUrl: null
    }],
    certificates: [{ id: 'cert-1', certificateType: 'Iso9001', serialNumber: 'S1', issuer: 'TÜV', validUntil: '2027-01-01' }],
    personnel: [{ id: 'per-1', fullName: 'Maria Ionescu', role: 'Inginer RTE', professionalCertificates: ['Autorizație RTE'], cvUrl: null }]
  };

  function mount(overrides: Record<string, unknown> = {}) {
    const service = {
      get: vi.fn().mockReturnValue(of(qualification)),
      updateLegal: vi.fn(),
      addExperience: vi.fn().mockReturnValue(of({ ...qualification.experience[0], id: 'exp-2', contractTitle: 'Contract nou' })),
      updateExperience: vi.fn(),
      removeExperience: vi.fn().mockReturnValue(of(undefined)),
      addCertificate: vi.fn(),
      updateCertificate: vi.fn(),
      removeCertificate: vi.fn().mockReturnValue(of(undefined)),
      addPersonnel: vi.fn(),
      updatePersonnel: vi.fn(),
      removePersonnel: vi.fn().mockReturnValue(of(undefined)),
      ...overrides
    };
    TestBed.configureTestingModule({
      imports: [CompanyQualificationComponent],
      providers: [{ provide: CompanyQualificationService, useValue: service }]
    });
    const fixture = TestBed.createComponent(CompanyQualificationComponent);
    fixture.componentRef.setInput('companyId', 'company-1');
    fixture.detectChanges();
    return { fixture, service };
  }

  it('loads and renders the four qualification cards with legal data, portfolio, certificates and team', () => {
    const { fixture } = mount();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Calificare & Date Firmă (SEAP)');
    expect(text).toContain('Date Legale & Administrative');
    expect(text).toContain('Portofoliu & Experiență Similară');
    expect(text).toContain('Certificate & Atestate');
    expect(text).toContain('Echipă & Experți Cheie');
    // Legal data lives in <input> values (not textContent) — assert the actual input values.
    const repInput = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input'))
      .find(input => input.placeholder === 'ex: Ion Popescu');
    expect(repInput?.value).toBe('Ion Popescu');
    expect(text).toContain('BCR · RO49BCR123456789 · RON');
    expect(text).toContain('Reabilitare drum');
    expect(text).toContain('Primăria Udești');
    expect(text).toContain('ISO 9001');
    expect(text).toContain('Maria Ionescu');
    expect(text).toContain('Inginer RTE');
  });

  it('adds an experience contract through the form and updates the list', () => {
    const addExperience = vi.fn().mockReturnValue(of({ ...qualification.experience[0], id: 'exp-2', contractTitle: 'Contract nou', clientName: 'Primăria Cluj' }));
    const { fixture, service } = mount({ addExperience });

    const addButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.includes('Adaugă contract'))!;
    addButton.click();
    fixture.detectChanges();

    // The experience form is the first .form block; its 8 inputs start with Client and Title.
    const form = (fixture.nativeElement as HTMLElement).querySelectorAll('.form')[0]!;
    const inputs = form.querySelectorAll<HTMLInputElement>('input');
    inputs[0].value = 'Primăria Cluj';
    inputs[0].dispatchEvent(new Event('input'));
    inputs[1].value = 'Contract nou';
    inputs[1].dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const save = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.includes('Adaugă contractul'))!;
    save.click();
    fixture.detectChanges();

    expect(addExperience).toHaveBeenCalledWith('company-1', expect.objectContaining({ clientName: 'Primăria Cluj', contractTitle: 'Contract nou' }));
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Contractul a fost adăugat.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Contract nou');
  });
});
