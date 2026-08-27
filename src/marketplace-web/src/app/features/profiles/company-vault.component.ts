import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { CompanyVaultService, VaultDocumentView } from '../../core/companies/company-vault.service';

@Component({
  selector: 'app-company-vault',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:grid;gap:1rem}.card{padding:1.15rem 1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface)}.card h2{margin:0 0 .5rem;font-size:1.05rem}.upload{display:flex;gap:.7rem;flex-wrap:wrap;align-items:center}.upload input{flex:1 1 18rem}.upload button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}.upload button:disabled{opacity:.55;cursor:not-allowed}.doc{margin-top:.6rem;padding:.8rem .95rem;border:1px solid var(--border);border-radius:.8rem;background:var(--warm-white)}.doc strong{display:block}.doc small{color:var(--muted)}.pill{display:inline-flex;padding:.25rem .6rem;border-radius:999px;font-size:.74rem;font-weight:800}.pill.ok{color:#176b52;background:#e3f2ea}.pill.pending{color:#7a5206;background:#faf0d8}.note{padding:.8rem .95rem;border-radius:.7rem;background:#edf6f1;color:#315947;line-height:1.5}.note.error{background:#fff0f0;color:#8b2222}`],
  template: `<section class="card">
    <h2>Company Vault — documente pentru ofertare automată</h2>
    <p>Încarcă PDF-uri (certificări, experiență, devize). AI le citește la generarea ofertelor pentru anunțurile SEAP.</p>
    <div class="upload"><input type="file" accept="application/pdf" (change)="onFileSelected($any($event.target).files?.[0])" aria-label="Alege un fișier PDF">
      <button type="button" [disabled]="!selectedFile() || busy()" (click)="upload()">{{ busy() ? 'Se încarcă…' : 'Încarcă PDF' }}</button></div>
    @if (message()) { <p class="note" [class.error]="messageError()" role="status">{{ message() }}</p> }
    @for (document of documents(); track document.id) {
      <div class="doc"><strong>{{ document.fileName }}</strong>
        <small>Încărcat {{ document.uploadedAt | date:'dd.MM.yyyy HH:mm' }} · {{ document.chunkCount }} fragmente</small>
        <span class="pill" [class.ok]="document.extracted" [class.pending]="!document.extracted">{{ document.extracted ? 'Procesat' : 'În așteptare' }}</span></div>
    } @empty { <p class="note">Nu ai încă documente în vault.</p> }
  </section>`
})
export class CompanyVaultComponent {
  readonly companyId = input.required<string>();
  private readonly service = inject(CompanyVaultService);
  readonly documents = signal<VaultDocumentView[]>([]);
  readonly selectedFile = signal<File | null>(null);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly messageError = signal(false);

  constructor() {
    // The required input is only available after Angular applies inputs; reading it in the
    // constructor throws NG0950 and blanks the whole page. Load inside an effect instead.
    effect(() => this.load());
  }

  onFileSelected(file: File | undefined): void {
    this.selectedFile.set(file && file.size > 0 ? file : null);
    this.message.set('');
  }

  load(): void {
    this.service.list(this.companyId()).subscribe({
      next: value => this.documents.set(value),
      error: () => { this.messageError.set(true); this.message.set('Documentele nu au putut fi încărcate.'); }
    });
  }

  upload(): void {
    const file = this.selectedFile();
    if (!file || this.busy()) return;
    this.busy.set(true);
    this.message.set('');
    this.service.upload(this.companyId(), file).subscribe({
      next: () => { this.busy.set(false); this.selectedFile.set(null); this.messageError.set(false); this.message.set('Documentul a fost încărcat.'); this.load(); },
      error: error => { this.busy.set(false); this.messageError.set(true); this.message.set(error?.error?.errors?.document?.[0] ?? 'Documentul nu a putut fi încărcat. Încearcă din nou.'); }
    });
  }
}
