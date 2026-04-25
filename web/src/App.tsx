import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import { useBootstrap } from "./auth/useBootstrap";
import Home from "./pages/Home";
import Login from "./pages/Login";
import NewQuiz from "./pages/NewQuiz";
import Play from "./pages/Play";
import QuizDetail from "./pages/QuizDetail";
import Register from "./pages/Register";
import SharedSession from "./pages/SharedSession";

export default function App() {
  // Attempt to rehydrate the session via the refresh cookie on first load.
  useBootstrap();

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login"        element={<Login />} />
        <Route path="/register"     element={<Register />} />
        {/* Public no-auth view of a completed session. */}
        <Route path="/share/:token" element={<SharedSession />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Home />
            </ProtectedRoute>
          }
        />
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
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
