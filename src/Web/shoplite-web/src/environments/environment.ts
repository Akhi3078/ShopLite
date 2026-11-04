/**
 * API locations for local development.
 *
 * These are the fixed ports from each service's Properties/launchSettings.json, which
 * Aspire reuses when it starts them. They are hardcoded deliberately: the alternative
 * is build-time variable substitution, which adds tooling for no benefit while every
 * service runs on this machine.
 *
 * Phase 2 adds the YARP gateway, after which both of these collapse into a single
 * origin and this file becomes one line.
 */
export const environment = {
  identityUrl: 'https://localhost:7130',
  catalogUrl: 'https://localhost:7232',
};
