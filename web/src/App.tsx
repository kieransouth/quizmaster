import { BrowserRouter, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import { useAuthStore } from "./auth/store";
import { useBootstrap } from "./auth/useBootstrap";
import Home from "./pages/Home";
import Landing from "./pages/Landing";
import Login from "./pages/Login";
import NewQuiz from "./pages/NewQuiz";
import NotFound from "./pages/NotFound";
import Play from "./pages/Play";
import QuizDetail from "./pages/QuizDetail";
import Register from "./pages/Register";
import Settings from "./pages/Settings";
import SharedSession from "./pages/SharedSession";
import { DesktopOnlyBanner } from "./ui/DesktopOnlyBanner";
import { LoadingShell } from "./ui/LoadingShell";

export default function App() {
  // Attempt to rehydrate the session via the refresh cookie on first load.
  useBootstrap();

  return (
    <BrowserRouter>
      <DesktopOnlyBanner />
      <Routes>
        <Route path="/login"        element={<Login />} />
        <Route path="/register"     element={<Register />} />
        {/* Public no-auth view of a completed session. */}
        <Route path="/share/:token" element={<SharedSession />} />
        {/* Root branches on auth: dashboard for signed-in, marketing
            landing for everyone else (including crawlers). */}
        <Route path="/" element={<Root />} />
        <Route
          path="/quizzes/new"
          element={
            <ProtectedRoute>
              <NewQuiz />
            </ProtectedRoute>
          }
        />
        <Route
          path="/quizzes/:id"
          element={
            <ProtectedRoute>
              <QuizDetail />
            </ProtectedRoute>
          }
        />
        <Route
          path="/play/:id"
          element={
            <ProtectedRoute>
              <Play />
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings"
          element={
            <ProtectedRoute>
              <Settings />
            </ProtectedRoute>
          }
        />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  );
}

function Root() {
  const bootstrapped = useAuthStore((s) => s.bootstrapped);
  const user         = useAuthStore((s) => s.user);
  if (!bootstrapped) return <LoadingShell />;
  return user ? <Home /> : <Landing />;
}
