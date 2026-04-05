import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { HashRouter, Routes, Route } from "react-router-dom";
import "./index.css";
import Layout from "./components/Layout";
import Home from "./pages/Home";
import Downloads from "./pages/Downloads";
import Changelog from "./pages/Changelog";
import Donate from "./pages/Donate";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <HashRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<Home />} />
          <Route path="/downloads" element={<Downloads />} />
          <Route path="/changelog" element={<Changelog />} />
          <Route path="/donate" element={<Donate />} />
        </Route>
      </Routes>
    </HashRouter>
  </StrictMode>
);
