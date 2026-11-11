import 'server-only';
import { headers } from 'next/headers';

// Link-preview bots (WhatsApp, Slack, ...) are never signed in. They must get the invite PAGE, not a redirect, so its metadata is read.
const BOTS = /whatsapp|facebookexternalhit|twitterbot|slackbot|discordbot|telegrambot|linkedinbot|skypeuripreview|crawler|spider|preview/i;

export async function isPreviewBot(): Promise<boolean> {
  return BOTS.test((await headers()).get('user-agent') ?? '');
}