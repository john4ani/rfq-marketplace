import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import {
  BankAccount, CompanyCertificate, COMPANY_CERTIFICATE_TYPES, COMPANY_CERTIFICATE_TYPE_LABELS,
  CompanyExperience, CompanyPersonnel, CompanyQualification, CompanyQualificationService
} from '../../core/companies/company-qualification.service';

@Component({
  selector: 'app-company-qualification',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:grid;gap:1rem}.qualification{display:grid;gap:1rem}.qualification-head h2{margin:0;font-size:1.1rem}.qualification-head p{margin:.25rem 0 0;color:var(--muted);font-size:.88rem;line-height:1.5}.card{padding:1.15rem 1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface)}.card h3{margin:0;font-size:1.02rem}.card h4{margin:1rem 0 .4rem;font-size:.85rem;color:var(--forest-light);text-transform:uppercase;letter-spacing:.05em}.card-head{display:flex;align-items:center;justify-content:space-between;gap:.7rem;flex-wrap:wrap;margin-bottom:.6rem}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(13rem,1fr));gap:.6rem}.grid label{display:grid;gap:.25rem;font-size:.82rem;font-weight:700;color:var(--muted)}.grid input,.grid select,.bank-add input,.bank-add select{min-height:2.4rem;padding:.45rem .6rem;border:1px solid var(--border);border-radius:.55rem;background:var(--warm-white);font:inherit}.bank-add{display:flex;gap:.5rem;flex-wrap:wrap;align-items:center;margin-bottom:.5rem}.bank-add input{flex:1 1 10rem}.row{display:flex;align-items:center;justify-content:space-between;gap:.8rem;padding:.65rem .8rem;border:1px solid var(--border);border-radius:.7rem;background:var(--warm-white);margin-top:.45rem}.row>span{display:grid;gap:.1rem;overflow:hidden}.row strong{font-size:.9rem}.row small{color:var(--muted);font-size:.78rem}.item-actions{display:flex;gap:.4rem;flex:0 0 auto}.form{display:grid;gap:.7rem;padding:.8rem;border:1px solid var(--border);border-radius:.7rem;background:var(--warm-white);margin-top:.5rem}button{min-height:2.5rem;padding:.55rem 1rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer;font-size:.88rem}button:disabled{opacity:.55;cursor:not-allowed}.item-actions button{min-height:2rem;padding:.35rem .7rem;font-size:.78rem;background:var(--forest-light)}.note{padding:.8rem .95rem;border-radius:.7rem;background:#edf6f1;color:#315947;line-height:1.5}.note.error{background:#fff0f0;color:#8b2222}.empty{color:var(--muted);font-style:italic;font-size:.85rem;margin:.5rem 0 0}`],
  template: `<section class="qualification">
    <div class="qualification-head"><h2>Calificare &amp; Date Firmă (SEAP)</h2>
      <p>Datele legale, experiența similară, certificatele și echipa ta alimentează propunerile tehnice generate pentru anunțurile SEAP.</p></div>

    @if (loading()) { <p class="note" role="status">Se încarcă calificarea…</p> }
    @else if (loadError()) {
      <p class="note error" role="alert">Calificarea nu a putut fi încărcată.</p>
      <button type="button" (click)="load()">Reîncearcă</button>
    }
    @else if (qualification(); as q) {
      @if (message()) { <p class="note" [class.error]="messageError()" role="status">{{ message() }}</p> }

      <section class="card">
        <div class="card-head"><h3>Date Legale &amp; Administrative</h3></div>
        <div class="grid">
          <label>Reprezentant legal<input [value]="legalName()" (input)="legalName.set($any($event.target).value)" placeholder="ex: Ion Popescu" /></label>
          <label>Rol<input [value]="legalRole()" (input)="legalRole.set($any($event.target).value)" placeholder="ex: Administrator" /></label>
          <label>Formă juridică<input [value]="legalFormValue()" (input)="legalFormValue.set($any($event.target).value)" placeholder="ex: SRL" /></label>
        </div>
        <h4>Conturi IBAN</h4>
        <div class="bank-add">
          <input [value]="bankName()" (input)="bankName.set($any($event.target).value)" placeholder="Bancă" />
          <input [value]="bankIban()" (input)="bankIban.set($any($event.target).value)" placeholder="IBAN" />
          <select [value]="bankCurrency()" (change)="bankCurrency.set($any($event.target).value)"><option value="RON">RON</option><option value="EUR">EUR</option><option value="USD">USD</option></select>
          <button type="button" (click)="addBankAccount()">Adaugă cont</button>
        </div>
        @for (account of bankAccounts(); track account.iban) {
          <div class="row"><span><strong>{{ account.bankName }} · {{ account.iban }} · {{ account.currency }}</strong></span>
            <div class="item-actions"><button type="button" (click)="removeBankAccount(account)">Șterge</button></div></div>
        } @empty { <p class="empty">Niciun cont adăugat.</p> }
        <div style="margin-top:.7rem"><button type="button" [disabled]="busy()" (click)="saveLegal()">{{ busy() ? 'Se salvează…' : 'Salvează datele legale' }}</button></div>
      </section>


      <section class="card">
        <div class="card-head"><h3>Portofoliu &amp; Experiență Similară</h3>
          <button type="button" (click)="toggleExperienceForm()">{{ experienceFormVisible() ? 'Renunță' : 'Adaugă contract' }}</button></div>
        @if (experienceFormVisible()) {
          <div class="form">
            <div class="grid">
              <label>Client<input [value]="expClient()" (input)="expClient.set($any($event.target).value)" /></label>
              <label>Titlul contractului<input [value]="expContract()" (input)="expContract.set($any($event.target).value)" /></label>
              <label>Nr. / dată contract<input [value]="expNumberDate()" (input)="expNumberDate.set($any($event.target).value)" placeholder="ex: 123/12.03.2024" /></label>
              <label>Valoare<input type="number" min="0" [value]="expAmount()" (input)="setExperienceAmount($any($event.target).value)" /></label>
              <label>Valută<input [value]="expCurrency()" (input)="expCurrency.set($any($event.target).value)" placeholder="RON" /></label>
              <label>Cod CPV<input [value]="expCpv()" (input)="expCpv.set($any($event.target).value)" placeholder="ex: 45233120-6" /></label>
              <label>Data finalizării<input type="date" [value]="expCompletion()" (input)="expCompletion.set($any($event.target).value)" /></label>
              <label>Link document<input [value]="expDocUrl()" (input)="expDocUrl.set($any($event.target).value)" placeholder="https://…" /></label>
            </div>
            <button type="button" [disabled]="busy()" (click)="saveExperience()">{{ editingExperienceId() ? 'Salvează modificările' : 'Adaugă contractul' }}</button>
          </div>
        }
        @for (item of q.experience; track item.id) {
          <div class="row"><span><strong>{{ item.contractTitle }}</strong><small>{{ item.clientName }}{{ item.valueAmount !== null ? ' · ' + item.valueAmount + ' ' + (item.valueCurrency ?? '') : '' }}{{ item.completionDate ? ' · finalizat ' + item.completionDate : '' }}</small></span>
            <div class="item-actions"><button type="button" (click)="editExperience(item)">Editează</button><button type="button" (click)="removeExperience(item.id)">Șterge</button></div></div>
        } @empty { <p class="empty">Niciun contract adăugat.</p> }
      </section>

      <section class="card">
        <div class="card-head"><h3>Certificate &amp; Atestate</h3>
          <button type="button" (click)="toggleCertificateForm()">{{ certificateFormVisible() ? 'Renunță' : 'Adaugă certificat' }}</button></div>
        @if (certificateFormVisible()) {
          <div class="form">
            <div class="grid">
              <label>Tip<select [value]="certType()" (change)="certType.set($any($event.target).value)">
                @for (type of certificateTypes; track type) { <option [value]="type">{{ certificateTypeLabel(type) }}</option> }
              </select></label>
              <label>Număr serie<input [value]="certSerial()" (input)="certSerial.set($any($event.target).value)" /></label>
              <label>Emitent<input [value]="certIssuer()" (input)="certIssuer.set($any($event.target).value)" /></label>
              <label>Valabil până la<input type="date" [value]="certValidUntil()" (input)="certValidUntil.set($any($event.target).value)" /></label>
            </div>
            <button type="button" [disabled]="busy()" (click)="saveCertificate()">{{ editingCertificateId() ? 'Salvează modificările' : 'Adaugă certificatul' }}</button>
          </div>
        }
        @for (item of q.certificates; track item.id) {
          <div class="row"><span><strong>{{ certificateTypeLabel(item.certificateType) }}</strong><small>{{ certificateDetails(item) }}</small></span>
            <div class="item-actions"><button type="button" (click)="editCertificate(item)">Editează</button><button type="button" (click)="removeCertificate(item.id)">Șterge</button></div></div>
        } @empty { <p class="empty">Niciun certificat adăugat.</p> }
      </section>

      <section class="card">
        <div class="card-head"><h3>Echipă &amp; Experți Cheie</h3>
          <button type="button" (click)="togglePersonnelForm()">{{ personnelFormVisible() ? 'Renunță' : 'Adaugă expert' }}</button></div>
        @if (personnelFormVisible()) {
          <div class="form">
            <div class="grid">
              <label>Nume complet<input [value]="personName()" (input)="personName.set($any($event.target).value)" /></label>
              <label>Rol<input [value]="personRole()" (input)="personRole.set($any($event.target).value)" placeholder="ex: Inginer RTE" /></label>
              <label>Calificări (separate prin virgulă)<input [value]="personCerts()" (input)="personCerts.set($any($event.target).value)" /></label>
              <label>Link CV<input [value]="personCvUrl()" (input)="personCvUrl.set($any($event.target).value)" placeholder="https://…" /></label>
            </div>
            <button type="button" [disabled]="busy()" (click)="savePersonnel()">{{ editingPersonnelId() ? 'Salvează modificările' : 'Adaugă expertul' }}</button>
          </div>
        }
        @for (item of q.personnel; track item.id) {
          <div class="row"><span><strong>{{ item.fullName }} — {{ item.role }}</strong><small>{{ item.professionalCertificates.join(', ') || 'Fără calificări listate' }}{{ item.cvUrl ? ' · ' + item.cvUrl : '' }}</small></span>
            <div class="item-actions"><button type="button" (click)="editPersonnel(item)">Editează</button><button type="button" (click)="removePersonnel(item.id)">Șterge</button></div></div>
        } @empty { <p class="empty">Niciun expert adăugat.</p> }
      </section>
    }
  </section>`
})
export class CompanyQualificationComponent {

  readonly companyId = input.required<string>();
  private readonly service = inject(CompanyQualificationService);

  readonly qualification = signal<CompanyQualification | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly messageError = signal(false);
  readonly certificateTypes = COMPANY_CERTIFICATE_TYPES;

  // Legal profile form.
  readonly legalName = signal('');
  readonly legalRole = signal('');
  readonly legalFormValue = signal('');
  readonly bankAccounts = signal<BankAccount[]>([]);
  readonly bankName = signal('');
  readonly bankIban = signal('');
  readonly bankCurrency = signal('RON');

  // Experience form.
  readonly experienceFormVisible = signal(false);
  readonly editingExperienceId = signal<string | null>(null);
  readonly expClient = signal('');
  readonly expContract = signal('');
  readonly expNumberDate = signal('');
  readonly expAmount = signal<number | null>(null);
  readonly expCurrency = signal('RON');
  readonly expCpv = signal('');
  readonly expCompletion = signal('');
  readonly expDocUrl = signal('');

  // Certificate form.
  readonly certificateFormVisible = signal(false);
  readonly editingCertificateId = signal<string | null>(null);
  readonly certType = signal<string>('Iso9001');
  readonly certSerial = signal('');
  readonly certIssuer = signal('');
  readonly certValidUntil = signal('');

  // Personnel form.
  readonly personnelFormVisible = signal(false);
  readonly editingPersonnelId = signal<string | null>(null);
  readonly personName = signal('');
  readonly personRole = signal('');
  readonly personCerts = signal('');
  readonly personCvUrl = signal('');

  constructor() {
    // The required input is only available after Angular applies inputs; load inside an effect.
    effect(() => this.load());
  }

  certificateTypeLabel(type: string): string {
    return COMPANY_CERTIFICATE_TYPE_LABELS[type] ?? type;
  }

  certificateDetails(item: CompanyCertificate): string {
    return [item.serialNumber, item.issuer, item.validUntil ? 'valabil până ' + item.validUntil : null]
      .filter(Boolean)
      .join(' · ');
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.service.get(this.companyId()).subscribe({
      next: value => {
        this.qualification.set(value);
        this.syncLegalForm(value.legal);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  private syncLegalForm(legal: CompanyQualification['legal']): void {
    this.legalName.set(legal?.legalRepresentativeName ?? '');
    this.legalRole.set(legal?.legalRepresentativeRole ?? '');
    this.legalFormValue.set(legal?.legalForm ?? '');
    this.bankAccounts.set(legal?.bankAccounts ?? []);
  }

  addBankAccount(): void {
    const bankName = this.bankName().trim();
    const iban = this.bankIban().trim().toUpperCase();
    const currency = this.bankCurrency().trim().toUpperCase() || 'RON';
    if (!bankName || !iban) return;
    this.bankAccounts.update(list => [...list, { bankName, iban, currency }]);
    this.bankName.set('');
    this.bankIban.set('');
  }

  removeBankAccount(account: BankAccount): void {
    this.bankAccounts.update(list => list.filter(item => item !== account));
  }

  saveLegal(): void {
    const representativeName = this.legalName().trim();
    if (!representativeName) { this.showError('Completează numele reprezentantului legal.'); return; }
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.service.updateLegal(this.companyId(), {
      legalRepresentativeName: representativeName,
      legalRepresentativeRole: this.legalRole().trim() || null,
      legalForm: this.legalFormValue().trim() || null,
      bankAccounts: this.bankAccounts()
    }).subscribe({
      next: saved => {
        this.busy.set(false);
        this.qualification.update(q => q ? { ...q, legal: saved } : q);
        this.message.set('Datele legale au fost salvate.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Datele legale nu au putut fi salvate.');
      }
    });
  }

  toggleExperienceForm(): void {
    if (this.experienceFormVisible()) this.resetExperienceForm();
    this.experienceFormVisible.set(!this.experienceFormVisible());
  }

  editExperience(item: CompanyExperience): void {
    this.experienceFormVisible.set(true);
    this.editingExperienceId.set(item.id);
    this.expClient.set(item.clientName);
    this.expContract.set(item.contractTitle);
    this.expNumberDate.set(item.contractNumberDate ?? '');
    this.expAmount.set(item.valueAmount);
    this.expCurrency.set(item.valueCurrency ?? 'RON');
    this.expCpv.set(item.cpvCode ?? '');
    this.expCompletion.set(item.completionDate ?? '');
    this.expDocUrl.set(item.documentRefUrl ?? '');
  }

  setExperienceAmount(value: string): void {
    const parsed = value === '' ? null : Number(value);
    this.expAmount.set(parsed !== null && Number.isFinite(parsed) && parsed >= 0 ? parsed : null);
  }

  saveExperience(): void {
    const editingId = this.editingExperienceId();
    const input = {
      clientName: this.expClient().trim(),
      contractTitle: this.expContract().trim(),
      contractNumberDate: this.expNumberDate().trim() || null,
      valueAmount: this.expAmount(),
      valueCurrency: this.expCurrency().trim() || null,
      cpvCode: this.expCpv().trim() || null,
      completionDate: this.expCompletion() || null,
      documentRefUrl: this.expDocUrl().trim() || null
    };
    if (!input.clientName || !input.contractTitle) { this.showError('Completează clientul și titlul contractului.'); return; }
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    const request = editingId
      ? this.service.updateExperience(this.companyId(), editingId, input)
      : this.service.addExperience(this.companyId(), input);
    request.subscribe({
      next: saved => {
        this.busy.set(false);
        this.qualification.update(q => q ? {
          ...q,
          experience: editingId
            ? q.experience.map(item => item.id === editingId ? saved : item)
            : [...q.experience, saved]
        } : q);
        this.resetExperienceForm();
        this.message.set(editingId ? 'Contractul a fost actualizat.' : 'Contractul a fost adăugat.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Contractul nu a putut fi salvat.');
      }
    });
  }

  removeExperience(id: string): void {
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.service.removeExperience(this.companyId(), id).subscribe({
      next: () => {
        this.busy.set(false);
        this.qualification.update(q => q ? { ...q, experience: q.experience.filter(item => item.id !== id) } : q);
        this.message.set('Contractul a fost șters.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Contractul nu a putut fi șters.');
      }
    });
  }

  private resetExperienceForm(): void {
    this.editingExperienceId.set(null);
    this.expClient.set('');
    this.expContract.set('');
    this.expNumberDate.set('');
    this.expAmount.set(null);
    this.expCurrency.set('RON');
    this.expCpv.set('');
    this.expCompletion.set('');
    this.expDocUrl.set('');
  }

  toggleCertificateForm(): void {
    if (this.certificateFormVisible()) this.resetCertificateForm();
    this.certificateFormVisible.set(!this.certificateFormVisible());
  }

  editCertificate(item: CompanyCertificate): void {
    this.certificateFormVisible.set(true);
    this.editingCertificateId.set(item.id);
    this.certType.set(item.certificateType);
    this.certSerial.set(item.serialNumber ?? '');
    this.certIssuer.set(item.issuer ?? '');
    this.certValidUntil.set(item.validUntil ?? '');
  }

  saveCertificate(): void {
    const editingId = this.editingCertificateId();
    const input = {
      certificateType: this.certType(),
      serialNumber: this.certSerial().trim() || null,
      issuer: this.certIssuer().trim() || null,
      validUntil: this.certValidUntil() || null
    };
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    const request = editingId
      ? this.service.updateCertificate(this.companyId(), editingId, input)
      : this.service.addCertificate(this.companyId(), input);
    request.subscribe({
      next: saved => {
        this.busy.set(false);
        this.qualification.update(q => q ? {
          ...q,
          certificates: editingId
            ? q.certificates.map(item => item.id === editingId ? saved : item)
            : [...q.certificates, saved]
        } : q);
        this.resetCertificateForm();
        this.message.set(editingId ? 'Certificatul a fost actualizat.' : 'Certificatul a fost adăugat.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Certificatul nu a putut fi salvat.');
      }
    });
  }

  removeCertificate(id: string): void {
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.service.removeCertificate(this.companyId(), id).subscribe({
      next: () => {
        this.busy.set(false);
        this.qualification.update(q => q ? { ...q, certificates: q.certificates.filter(item => item.id !== id) } : q);
        this.message.set('Certificatul a fost șters.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Certificatul nu a putut fi șters.');
      }
    });
  }

  private resetCertificateForm(): void {
    this.editingCertificateId.set(null);
    this.certType.set('Iso9001');
    this.certSerial.set('');
    this.certIssuer.set('');
    this.certValidUntil.set('');
  }

  togglePersonnelForm(): void {
    if (this.personnelFormVisible()) this.resetPersonnelForm();
    this.personnelFormVisible.set(!this.personnelFormVisible());
  }

  editPersonnel(item: CompanyPersonnel): void {
    this.personnelFormVisible.set(true);
    this.editingPersonnelId.set(item.id);
    this.personName.set(item.fullName);
    this.personRole.set(item.role);
    this.personCerts.set(item.professionalCertificates.join(', '));
    this.personCvUrl.set(item.cvUrl ?? '');
  }

  savePersonnel(): void {
    const editingId = this.editingPersonnelId();
    const input = {
      fullName: this.personName().trim(),
      role: this.personRole().trim(),
      professionalCertificates: this.personCerts().split(',').map(item => item.trim()).filter(Boolean),
      cvUrl: this.personCvUrl().trim() || null
    };
    if (!input.fullName || !input.role) { this.showError('Completează numele și rolul expertului.'); return; }
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    const request = editingId
      ? this.service.updatePersonnel(this.companyId(), editingId, input)
      : this.service.addPersonnel(this.companyId(), input);
    request.subscribe({
      next: saved => {
        this.busy.set(false);
        this.qualification.update(q => q ? {
          ...q,
          personnel: editingId
            ? q.personnel.map(item => item.id === editingId ? saved : item)
            : [...q.personnel, saved]
        } : q);
        this.resetPersonnelForm();
        this.message.set(editingId ? 'Expertul a fost actualizat.' : 'Expertul a fost adăugat.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Expertul nu a putut fi salvat.');
      }
    });
  }

  removePersonnel(id: string): void {
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.service.removePersonnel(this.companyId(), id).subscribe({
      next: () => {
        this.busy.set(false);
        this.qualification.update(q => q ? { ...q, personnel: q.personnel.filter(item => item.id !== id) } : q);
        this.message.set('Expertul a fost șters.');
      },
      error: () => {
        this.busy.set(false);
        this.showError('Expertul nu a putut fi șters.');
      }
    });
  }

  private resetPersonnelForm(): void {
    this.editingPersonnelId.set(null);
    this.personName.set('');
    this.personRole.set('');
    this.personCerts.set('');
    this.personCvUrl.set('');
  }

  private showError(message: string): void {
    this.messageError.set(true);
    this.message.set(message);
  }
}
