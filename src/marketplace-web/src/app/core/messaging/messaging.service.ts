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

@Injectable({ providedIn: 'root' })
export class MessagingService {
  private readonly http = inject(HttpClient);

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
