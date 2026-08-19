import { ChangeDetectionStrategy, Component, computed, ElementRef, HostListener, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ConversationSummary, MessagingService } from '../../core/messaging/messaging.service';

// Unified Mesaje inbox trigger for the shell, sibling to the notifications bell. 🔔 says "something happened";
// 💬 is the conversation itself. Procurement-contextual only — it lists the User's own conversations (buying or
// representing a company); it is not a chat directory. Complements the contextual entry points, never replaces them.
@Component({
  selector: 'app-messages-menu',
  template: `<div class="messages">
    <button class="trigger" type="button" (click)="toggle()" [attr.aria-expanded]="open()" aria-label="Mesaje">
      <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
      <span class="trigger-label">Mesaje</span>
      @if (unreadTotal() > 0) { <span class="count">{{ unreadTotal() > 99 ? '99+' : unreadTotal() }}</span> }
    </button>
    @if (open()) {
      <section class="panel" aria-label="Panou mesaje">
        <header><strong>Mesaje</strong>@if (unreadTotal() > 0) { <small>{{ unreadTotal() }} necitite</small> }</header>
        @if (loading()) { <p class="state">Încărcăm mesajele…</p> }
        @else if (error()) { <div class="state error"><p>Nu am putut încărca mesajele.</p><button type="button" (click)="load()">Încearcă din nou</button></div> }
        @else if (conversations().length === 0) { <div class="state"><strong>Nu ai încă niciun mesaj.</strong><p>Conversațiile cu furnizorii și cumpărătorii vor apărea aici.</p></div> }
        @else {
          <div class="list">
            @for (item of conversations(); track item.offerId) {
              <button type="button" class="item" [class.unread]="item.unreadCount > 0" (click)="openConversation(item)">
                <span class="row"><strong>{{ item.otherPartyName }}</strong><time>{{ relativeTime(item.lastActivityAt) }}</time></span>
                <span class="context">{{ contextLabel(item) }}</span>
                @if (item.lastMessagePreview) { <span class="preview">{{ item.lastMessagePreview }}</span> }
                @if (item.unreadCount > 0) { <span class="unread-dot" aria-label="Mesaje necitite"></span> }
              </button>
            }
          </div>
        }
      </section>
    }
  </div>`,
  styles: [`:host{display:block}.messages{position:relative}.trigger{position:relative;display:flex;width:100%;min-height:2.75rem;align-items:center;gap:.7rem;padding:0 .8rem!important;border:0;border-radius:.7rem!important;color:rgb(255 255 255 / 88%)!important;background:transparent!important;font:inherit;font-weight:650;cursor:pointer}.trigger:hover,.trigger:focus-visible,.trigger[aria-expanded=true]{color:#fff!important;background:rgb(255 255 255 / 14%)!important}.trigger svg{width:1.2rem;flex:0 0 auto;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}.count{display:grid;min-width:1.25rem;height:1.25rem;margin-left:auto;place-items:center;padding:0 .18rem;border-radius:99px;background:#e0a64e;color:#1f2124;font-size:.65rem;font-weight:850}.panel{position:absolute;z-index:30;bottom:0;left:calc(100% + .65rem);width:min(24rem,calc(100vw - 1.5rem));overflow:hidden;border:1px solid #dce4df;border-radius:14px;background:white;box-shadow:0 18px 50px #17251c2b;color:#233129}.panel header{display:flex;align-items:center;justify-content:space-between;padding:1rem;border-bottom:1px solid #e8ece9}.panel header small{color:#68746d}.list{max-height:26rem;overflow:auto}.item{position:relative;display:grid!important;width:100%;gap:.25rem;padding:.85rem 1rem!important;border:0;border-bottom:1px solid #f0f3f1;border-radius:0!important;background:white!important;text-align:left;cursor:pointer}.item:hover{background:#f5f8f6!important}.item.unread{background:#edf7f2!important}.item .row{display:flex;align-items:baseline;justify-content:space-between;gap:.5rem}.item strong{color:#233129;font-size:.92rem}.item time{color:#8a938c;font-size:.72rem;white-space:nowrap}.item .context{color:#2f6b4f;font-size:.76rem;font-weight:650}.item .preview{overflow:hidden;color:#627068;font-size:.82rem;text-overflow:ellipsis;white-space:nowrap}.unread-dot{position:absolute;top:1rem;right:.75rem;width:.5rem;height:.5rem;border-radius:50%;background:#176b52}.state{display:grid;gap:.35rem;padding:1.5rem;color:#68746d;text-align:center}.state strong{color:#233129}.state.error button,.state button{justify-self:center;margin-top:.4rem;padding:.5rem .8rem!important;border:1px solid #176b52!important;border-radius:.5rem!important;color:#176b52!important;background:#fff!important;font-weight:700}@media(max-width:52rem){.trigger{width:100%;min-height:2.65rem;justify-content:flex-start}.trigger-label{display:inline}.panel{position:fixed;top:auto;bottom:5rem;right:.75rem;left:.75rem;width:auto}}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MessagesMenuComponent {
  private readonly service = inject(MessagingService);
  private readonly router = inject(Router);
  private readonly element = inject(ElementRef<HTMLElement>);
  readonly open = signal(false);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly conversations = signal<ConversationSummary[]>([]);
  readonly unreadTotal = computed(() => this.conversations().reduce((total, item) => total + item.unreadCount, 0));

  constructor() { this.load(); }

  toggle(): void { this.open.update(value => !value); if (this.open()) this.load(); }

  load(): void {
    this.loading.set(true); this.error.set(false);
    this.service.list().subscribe({
      next: values => { this.conversations.set(values); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set(true); }
    });
  }

  openConversation(item: ConversationSummary): void {
    this.open.set(false);
    void this.router.navigate(['/offers', item.offerId, 'conversation']);
  }

  contextLabel(item: ConversationSummary): string {
    const context = item.viewerRole === 'Buyer' ? 'Cumpărare' : (item.viewerCompanyName || 'Firma ta');
    return `${context} · ${item.rfqTitle}`;
  }

  relativeTime(value: string): string {
    const diff = Date.now() - new Date(value).getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return 'Acum';
    if (minutes < 60) return `Acum ${minutes} min`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `Acum ${hours} h`;
    const days = Math.floor(hours / 24);
    return days < 7 ? `Acum ${days} z` : new Date(value).toLocaleDateString('ro-RO');
  }

  @HostListener('document:click', ['$event']) closeOutside(event: MouseEvent): void {
    if (this.open() && !this.element.nativeElement.contains(event.target as Node)) this.open.set(false);
  }
}
