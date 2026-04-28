# 三端中英文切换实施指南

## ✅ 已完成

### buyer-web (商城端)
- ✅ i18n 核心系统创建完成
  - `src/shared/i18n/locales.ts` - 中英文翻译文件
  - `src/shared/i18n/I18nContext.tsx` - React Context Provider
  - `src/shared/ui/LanguageSwitcher.tsx` - 语言切换组件
- ✅ 已集成到 `main.tsx`
- ✅ 已在 `AppLayout.tsx` 顶部导航添加语言切换按钮

---

## 📋 待完成

### buyer-web 页面翻译
需要将以下页面中的硬编码中文替换为 `t('key')` 调用：

1. **LoginPage.tsx** - 登录页面
2. **CatalogPages.tsx** - 首页和商品列表
3. **TradePages.tsx** - 购物车
4. **OrderPages.tsx** - 订单
5. **MembershipPages.tsx** - 会员中心
6. **AssetsPage.tsx** - 资产中心
7. **ProfilePages.tsx** - 个人中心

**示例：**
```tsx
// 修改前
<h1>欢迎登录</h1>
<button>登录</button>

// 修改后
const { t } = useI18n()
<h1>{t('login.title')}</h1>
<button>{t('login.loginBtn')}</button>
```

---

### merchant-admin (商家管理后台)

#### 1. 创建 i18n 系统
```bash
# 创建文件
src/shared/i18n/locales.ts        # ✅ 已创建
src/shared/i18n/I18nContext.tsx   # 需要创建（复制 buyer-web 的版本）
src/shared/ui/LanguageSwitcher.tsx # 需要创建（复制 buyer-web 的版本）
```

**I18nContext.tsx 需要修改的地方：**
```tsx
// 修改 localStorage key
const saved = localStorage.getItem('merchant-admin-language')
localStorage.setItem('merchant-admin-language', lang)
```

#### 2. 集成到 main.tsx
```tsx
import { I18nProvider } from './shared/i18n/I18nContext'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <I18nProvider>
        <App />
      </I18nProvider>
    </BrowserRouter>
  </StrictMode>,
)
```

#### 3. 在 Shell.tsx 添加语言切换
```tsx
import { useI18n } from '../shared/i18n/I18nContext'
import { LanguageSwitcher } from '../shared/ui/LanguageSwitcher'

export function Shell() {
  const { t } = useI18n()
  
  return (
    <div className="app-layout">
      <aside className="sidebar">
        {/* 菜单项使用 t() */}
        <Link to="/">{t('nav.dashboard')}</Link>
        <Link to="/products">{t('nav.products')}</Link>
        {/* ... */}
      </aside>
      
      <header className="top-header">
        <div className="header-actions">
          <LanguageSwitcher />
          {/* ... */}
        </div>
      </header>
    </div>
  )
}
```

#### 4. 翻译所有页面
- LoginPage.tsx
- DashboardPage.tsx
- ProductPage.tsx
- OrdersPage.tsx
- MarketingPage.tsx
- ShopSettingsPage.tsx
- AuditPage.tsx

---

### platform-admin (平台管理后台)

#### 1. 创建 i18n 系统
```bash
# 创建文件
src/shared/i18n/locales.ts        # 需要创建
src/shared/i18n/I18nContext.tsx   # 需要创建
src/shared/ui/LanguageSwitcher.tsx # 需要创建
```

**locales.ts 需要包含的翻译：**
```typescript
export const zh_CN = {
  nav: {
    dashboard: '运营总览',
    merchant: '商家管理',
    catalog: '类目属性',
    productAudit: '商品审核',
    orderGovernance: '订单仲裁',
    userGovernance: '用户治理',
    marketing: '营销活动',
    finance: '财务结算',
    tickets: '客服工单',
    risk: '风控安全',
    system: '系统设置',
    audit: '操作审计',
    logout: '退出',
  },
  login: {
    title: '平台管理后台',
    subtitle: 'PLATFORM ADMIN PORTAL',
    accountLabel: '管理员账号',
    passwordLabel: '密码',
    loginBtn: '登录并进入',
  },
  dashboard: {
    title: '平台总览',
    // ... 统计卡片文本
  },
  // ... 其他页面翻译
}
```

#### 2. 集成到 main.tsx
同 merchant-admin

#### 3. 在 Shell.tsx 添加语言切换
同 merchant-admin

#### 4. 翻译所有页面
- LoginPage.tsx
- DashboardPage.tsx
- MerchantPage.tsx
- CatalogPage.tsx
- ProductAuditPage.tsx
- UserGovernancePage.tsx
- StubPages.tsx (包含8个子页面)

---

## 🔧 快速实施步骤

### 对于 merchant-admin 和 platform-admin：

#### Step 1: 复制 i18n 核心文件
```bash
# merchant-admin
cp buyer-web/src/shared/i18n/I18nContext.tsx merchant-admin/src/shared/i18n/
cp buyer-web/src/shared/ui/LanguageSwitcher.tsx merchant-admin/src/shared/ui/

# platform-admin
cp buyer-web/src/shared/i18n/I18nContext.tsx platform-admin/src/shared/i18n/
cp buyer-web/src/shared/ui/LanguageSwitcher.tsx platform-admin/src/shared/ui/
```

#### Step 2: 修改 localStorage key
在 I18nContext.tsx 中：
```tsx
// merchant-admin
const saved = localStorage.getItem('merchant-admin-language')
localStorage.setItem('merchant-admin-language', lang)

// platform-admin  
const saved = localStorage.getItem('platform-admin-language')
localStorage.setItem('platform-admin-language', lang)
```

#### Step 3: 集成 I18nProvider
在 main.tsx 中包裹 `<I18nProvider>`

#### Step 4: 添加语言切换按钮
在 Shell.tsx 或 AppLayout.tsx 的顶部导航添加 `<LanguageSwitcher />`

#### Step 5: 翻译页面
逐个页面将硬编码文本替换为 `t('key')` 调用

---

## 💡 使用示例

### 在组件中使用
```tsx
import { useI18n } from '../shared/i18n/I18nContext'

export function MyComponent() {
  const { t, language, setLanguage } = useI18n()
  
  return (
    <div>
      <h1>{t('page.title')}</h1>
      <p>{t('page.description')}</p>
      <button>{t('common.save')}</button>
    </div>
  )
}
```

### 添加新的翻译
在 locales.ts 中添加：
```typescript
export const zh_CN = {
  // ...
  newPage: {
    title: '新页面',
    description: '这是描述',
  },
}

export const en_US = {
  // ...
  newPage: {
    title: 'New Page',
    description: 'This is description',
  },
}
```

---

## 🎯 关键翻译键对照表

### buyer-web
| 中文 | 翻译键 |
|------|--------|
| 欢迎来到 Buyer Mall | home.welcome |
| 登录 | nav.login |
| 退出 | nav.logout |
| 购物车 | nav.cart |
| 我的订单 | nav.orders |
| 首页 | nav.home |
| 商品 | nav.products |
| 个人中心 | nav.profile |
| 会员中心 | nav.membership |
| 我的资产 | nav.assets |

### merchant-admin
| 中文 | 翻译键 |
|------|--------|
| 运营总览 | nav.dashboard |
| 商品管理 | nav.products |
| 订单售后 | nav.orders |
| 店铺营销 | nav.marketing |
| 店铺设置 | nav.settings |
| 操作记录 | nav.audit |

### platform-admin
| 中文 | 翻译键 |
|------|--------|
| 运营总览 | nav.dashboard |
| 商家管理 | nav.merchant |
| 类目属性 | nav.catalog |
| 商品审核 | nav.productAudit |
| 订单仲裁 | nav.orderGovernance |
| 用户治理 | nav.userGovernance |
| 营销活动 | nav.marketing |
| 财务结算 | nav.finance |
| 客服工单 | nav.tickets |
| 风控安全 | nav.risk |
| 系统设置 | nav.system |
| 操作审计 | nav.audit |

---

## ✅ 验收标准

- [ ] 三个项目都可在顶部导航看到语言切换按钮
- [ ] 点击按钮可切换中英文
- [ ] 切换后所有文本即时更新
- [ ] 刷新页面后语言设置保持
- [ ] 所有页面文本都已翻译
- [ ] 无硬编码中文文本残留
- [ ] 默认语言为中文
- [ ] 切换流畅无闪烁

---

**创建时间**: 2024-01-15  
**状态**: buyer-web 核心完成，待页面翻译；merchant-admin 和 platform-admin 待实施
