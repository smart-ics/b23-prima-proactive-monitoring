import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import { useAuthStore } from '@/app/stores/auth.store';
import { getSafeRedirect } from '@/shared/utils/security';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/features/auth/views/LoginView.vue'),
    meta: { requiresAuth: false, title: 'Sign In - PrIMA' },
  },
  {
    path: '/',
    component: () => import('@/app/layouts/AppLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'dashboard',
        component: () => import('@/features/dashboard/views/DashboardView.vue'),
        meta: { title: 'Dashboard - PrIMA', requiresAuth: true },
      },
      {
        path: 'issues/:id',
        name: 'issue-detail',
        component: () => import('@/features/issues/views/IssueDetailView.vue'),
        meta: { requiresAuth: true, title: 'Issue Detail - PrIMA' },
      },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore();
  // Ensure session is restored from localStorage before deciding
  if (!authStore.isAuthenticated) {
    authStore.restoreSession();
  }

  const isAuth = authStore.isAuthenticated;

  if (to.path === '/login') {
    if (isAuth) {
      const target = getSafeRedirect(to.query.redirect, '/');
      next(target === '/login' ? '/' : target);
    } else {
      next();
    }
  } else {
    if (!isAuth) {
      const safeRedirect = getSafeRedirect(to.fullPath, '/');
      next({
        path: '/login',
        query: safeRedirect !== '/' ? { redirect: safeRedirect } : undefined,
      });
    } else {
      next();
    }
  }
});

router.afterEach((to) => {
  if (to.meta.title) {
    document.title = to.meta.title as string;
  }
});

export default router;
