/**
 * The browser uses the gateway as its single API entry point in local development.
 * Aspire starts it on the fixed HTTPS port from ShopLite.Gateway's launch profile.
 */
export const environment = {
  gatewayUrl: 'https://localhost:7260',
};
