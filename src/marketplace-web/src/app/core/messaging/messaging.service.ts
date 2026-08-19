import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface ConversationMessage {
  id: string;
  body: string;
  createdAt: string;
  isMine: boolean;
}

export interface Conversation {
  offerId: string;
  rfqId: string;
  rfqTitle: string;
  otherPartyName: string;
  viewerRole: 'Buyer' | 'Supplier';
  unreadCount: number;
  messages: ConversationMessage[];
}

export interface ConversationSummary {
  offerId: string;
  rfqId: string;
  rfqTitle: string;
  otherPartyName: string;
  viewerRole: 'Buyer' | 'Supplier';
  viewerCompanyName: string | null;
  lastMessagePreview: string;
  lastActivityAt: string;
  unreadCount: number;
}

@Injectable({ providedIn: 'root' })
export class MessagingService {
  private readonly http = inject(HttpClient);

  list() { return this.http.get<ConversationSummary[]>('/api/v1/conversations'); }

  get(offerId: string, after?: string) {
    const options = after ? { params: new HttpParams().set('after', after) } : {};
    return this.http.get<Conversation>(`/api/v1/offers/${offerId}/conversation`, options);
  }

  send(offerId: string, body: string) {
    return this.http.post<ConversationMessage>(`/api/v1/offers/${offerId}/conversation/messages`, { body });
  }

  markRead(offerId: string) {
    return this.http.post<void>(`/api/v1/offers/${offerId}/conversation/read`, {});
  }
}
