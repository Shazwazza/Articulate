import type { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { type UmbNotificationColor, UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';

/**
 * Shows a global Umbraco notification.
 * @param {UmbLitElement} contextHost The LitElement instance (or other context consumer) that provides the notification context.
 * @param {string} message The message to display in the notification.
 * @param {UmbNotificationColor} type The color and style of the notification. Can be `''`, `'default'`, `'positive'`, `'warning'`, or `'danger'`.
 * @param {boolean} [stay=false] If true, the notification will stay until dismissed. If false, it will 'peek' and disappear automatically.
 * @returns {Promise<void>} A promise that resolves once the notification is shown.
 */
export async function showUmbracoNotification(
  contextHost: UmbLitElement,
  message: string,
  type: UmbNotificationColor,
  stay: boolean = false,
): Promise<void> {
  const notificationContext = await contextHost.getContext(UMB_NOTIFICATION_CONTEXT);
  if (!notificationContext) {
    console.error('UMB_NOTIFICATION_CONTEXT not found. Could not display notification.', {
      contextHost,
      message,
    });
    return;
  }
  if (!stay) {
    notificationContext.peek(type, {
      data: { message },
    });
  } else {
    notificationContext.stay(type, {
      data: { message },
    });
  }
}
