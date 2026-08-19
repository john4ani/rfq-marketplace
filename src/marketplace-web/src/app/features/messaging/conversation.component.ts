import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, interval, switchMap } from 'rxjs';
import { Conversation, ConversationMessage, MessagingService } from '../../core/messaging/messaging.service';

@Component({
  selector: 'app-conversation',
  imports: [RouterLink, DatePipe],
  template: `<main class="conversation-shell">
    @if (conversation(); as chat) {
      <header>
        <a [routerLink]="chat.viewerRole === 'Buyer' ? ['/rfqs', chat.rfqId, 'offers'] : ['/open-requests', chat.rfqId]">
          ← {{ chat.viewerRole === 'Buyer' ? 'Vezi ofertele' : 'Vezi cererea' }}
        </a>
        <div><p class="eyebrow">Conversație privată</p><h1>{{ chat.otherPartyName }}</h1><p><strong>Cerere:</strong> {{ chat.rfqTitle }}</p>
          <p class="participants">{{ conversationContext(chat.viewerRole) }}</p></div>
        <span class="privacy">Doar participanții ofertei</span>
      </header>

      <section class="messages" aria-label="Mesajele conversației" #messageList>
        @if (messages().length === 0) {
          <div class="empty">
            <span aria-hidden="true">✦</span>
            <h2>Începe conversația</h2>
            <p>Clarifică oferta, livrarea sau detaliile cererii. Mesajele rămân private între voi.</p>
          </div>
        } @else {
          @for (message of messages(); track message.id) {
            <article [class.mine]="message.isMine" [attr.aria-label]="senderLabel(message)">
              <div class="message-meta"><strong>{{ senderLabel(message) }}</strong>
                <time [attr.datetime]="message.createdAt">{{ message.createdAt | date:'dd.MM.yyyy, HH:mm' }}</time></div>
              <p>{{ message.body }}</p>
            </article>
          }
        }
      </section>

      <form (submit)="send($event)">
        <label for="message">Mesaj</label>
        <div class="composer">
          <textarea id="message" rows="2" maxlength="2000" [value]="draft()"
            (input)="draft.set($any($event.target).value)" placeholder="Scrie un mesaj clar și concis…"></textarea>
          <button type="submit" [disabled]="sending() || !draft().trim()">
            {{ sending() ? 'Se trimite…' : 'Trimite' }}
          </button>
        </div>
        <div class="form-meta"><small>{{ draft().length }}/2000</small>
          @if (sendError()) { <p role="alert">{{ sendError() }}</p> }</div>
      </form>
    } @else if (loading()) {
      <div class="loading" role="status"><span></span><span></span><p>Se încarcă conversația…</p></div>
    } @else {
      <section class="unavailable" role="alert">
        <h1>Conversația nu este disponibilă</h1>
        <p>{{ error() }}</p>
        <div class="unavailable-actions"><button type="button" (click)="loadInitial()">Încearcă din nou</button><a routerLink="/dashboard">Înapoi la contul meu</a></div>
      </section>
    }
  </main>`,
  styleUrl: './conversation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConversationComponent {
  private readonly service = inject(MessagingService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly offerId = inject(ActivatedRoute).snapshot.paramMap.get('offerId') ?? '';
  @ViewChild('messageList') private messageList?: ElementRef<HTMLElement>;

  readonly conversation = signal<Conversation | null>(null);
  readonly messages = signal<ConversationMessage[]>([]);
  readonly draft = signal('');
  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly error = signal('');
  readonly sendError = signal('');

  constructor() {
    this.loadInitial();
    interval(5000).pipe(
      takeUntilDestroyed(this.destroyRef),
      switchMap(() => {
        const last = this.messages().at(-1)?.createdAt;
        return this.conversation() ? this.service.get(this.offerId, last) : EMPTY;
      })
    ).subscribe({
      next: update => {
        if (update.messages.length) {
          this.append(update.messages);
          this.markRead();
        }
      }
    });
  }

  send(event: Event): void {
    event.preventDefault();
    const body = this.draft().trim();
    if (!body || this.sending()) return;
    this.sending.set(true);
    this.sendError.set('');
    this.service.send(this.offerId, body).subscribe({
      next: message => {
        this.append([message]);
        this.draft.set('');
        this.sending.set(false);
      },
      error: response => {
        this.sending.set(false);
        this.sendError.set(response.error?.errors?.message?.[0] ?? 'Mesajul nu a putut fi trimis. Încearcă din nou.');
      }
    });
  }

  conversationContext(role: Conversation['viewerRole']): string {
    return role === 'Buyer'
      ? 'Discuți despre o achiziție inițiată de tine.'
      : 'Discuți în numele firmei pe care o reprezinți.';
  }

  senderLabel(message: ConversationMessage): string {
    const chat = this.conversation();
    if (!chat) return message.isMine ? 'Tu' : 'Participant';
    return message.isMine ? 'Tu' : chat.otherPartyName;
  }

  loadInitial(): void {
    this.service.get(this.offerId).subscribe({
      next: conversation => {
        this.conversation.set(conversation);
        this.messages.set(conversation.messages);
        this.loading.set(false);
        if (conversation.unreadCount) this.markRead();
        this.scrollToLatest();
      },
      error: response => {
        this.loading.set(false);
        this.error.set(response.status === 404
          ? 'Oferta nu există sau nu participi la această conversație.'
          : 'Mesajele nu au putut fi încărcate. Reîncearcă din pagina ofertei.');
      }
    });
  }

  private markRead(): void {
    this.service.markRead(this.offerId).subscribe();
  }

  private append(incoming: ConversationMessage[]): void {
    const known = new Set(this.messages().map(message => message.id));
    this.messages.update(current => [...current, ...incoming.filter(message => !known.has(message.id))]);
    this.scrollToLatest();
  }

  private scrollToLatest(): void {
    queueMicrotask(() => this.messageList?.nativeElement.scrollTo?.({
      top: this.messageList.nativeElement.scrollHeight,
      behavior: 'smooth'
    }));
  }
}
