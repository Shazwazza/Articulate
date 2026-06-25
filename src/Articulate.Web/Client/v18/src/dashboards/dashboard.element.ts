import { css, customElement, html, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { UmbRoute, UmbRouterSlotInitEvent } from '@umbraco-cms/backoffice/router';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';

import BlogMlExporterElement from '../components/blogml-exporter.element.js';
import BlogMlImporterElement from '../components/blogml-importer.element.js';
import ThemeOptionsElement from '../components/theme-options.element.js';
import DashboardOptionsElement from '../components/dashboard-options.element.js';
import { HostStyles } from '../utils/style-utils.js';

/**
 * The main dashboard element for the Articulate package.
 * It sets up the routing for the different dashboard sections.
 *
 * @element articulate-dashboard
 * @extends {UmbLitElement}
 */
@customElement('articulate-dashboard')
export default class ArticulateDashboardElement extends UmbLitElement {
  /**
   * The base path for the router.
   * @private
   * @type {string | undefined}
   */
  @state() private _routerBasePath?: string;
  /**
   * The routes for the dashboard router.
   * @private
   * @type {UmbRoute[]}
   */
  @state() private _routes: UmbRoute[];

  constructor() {
    super();

    const createSetup = <T extends UmbLitElement & { routerPath?: string }>(component: new () => T) => {
      return (el: Element | undefined) => {
        if (this._routerBasePath && el instanceof component) {
          el.routerPath = this._routerBasePath;
        }
      };
    };

    this._routes = [
      {
        path: 'blogml/import',
        component: BlogMlImporterElement,
        setup: createSetup(BlogMlImporterElement),
      },
      {
        path: 'blogml/export',
        component: BlogMlExporterElement,
        setup: createSetup(BlogMlExporterElement),
      },
      {
        path: 'theme/options',
        component: ThemeOptionsElement,
        setup: createSetup(ThemeOptionsElement),
      },
      {
        path: '',
        component: DashboardOptionsElement,
        setup: createSetup(DashboardOptionsElement),
      },
      {
        path: '**',
        component: async () => (await import('@umbraco-cms/backoffice/router')).UmbRouteNotFoundElement,
      },
    ];
  }

  override render() {
    return html`
      <umb-body-layout>
        <div slot="header" class="header-container">
          <div class="articulate-header">
            <h1 class="header-title">Articulate Management</h1>
            <div class="header-logo">ã</div>
          </div>
        </div>
        <div class="dashboard-container">
          <umb-router-slot
            .routes=${this._routes}
            @init=${(event: UmbRouterSlotInitEvent) => {
              this._routerBasePath = event.target.absoluteRouterPath;
            }}></umb-router-slot>
        </div>
        <footer slot="footer">
          <p slot="footer-info" class="articulate-footer-info">Articulate | Version: ${import.meta.env.APP_VERSION}</p>
        </footer>
      </umb-body-layout>
    `;
  }

  /**
   * The styles for the component.
   * @static
   * @readonly
   */
  static override readonly styles = [
    UmbTextStyles,
    HostStyles,
    css`
      .dashboard-container {
        max-width: var(--uui-size-content-large);
        margin: 0 auto;
        padding: 0 var(--uui-size-space-3);
      }

      .header-container {
        width: 100%;
        padding: 0 var(--uui-size-space-3);
      }
      .header-title {
        font-size: var(--uui-type-h3-size);
        font-weight: 700;
        letter-spacing: 0.01em;
        color: var(--uui-color-text);
        display: flex;
        align-items: center;
        height: 100%;
      }
      .header-logo {
        font-weight: 900;
        font-size: var(--uui-type-h1-size);
        color: #c44;
        display: flex;
        align-items: center;
        justify-content: flex-end;
        height: 100%;
      }

      .articulate-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        width: 100%;
        height: 69px;
        background: var(--uui-color-surface);
        border-radius: var(--uui-border-radius);
        box-shadow: var(--uui-shadow-1);
        box-sizing: border-box;
        padding: 0 2rem;
        margin: 0;
        position: relative;
      }
      .articulate-footer-info {
        text-align: right;
        font-size: 0.8em;
        color: var(--uui-color-border-standalone);
      }

      @media (max-width: 768px) {
        .articulate-header {
          padding: 1rem 0.7rem;
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'articulate-dashboard': ArticulateDashboardElement;
  }
}
