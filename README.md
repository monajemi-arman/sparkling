# ✨ Sparkling ✨
Sparkling is an easy-to-use Apache Spark setup and management platform. Written in Typescript and C#, its main features are as following:
*   🚀 Setup Spark nodes automatically
*   👥 Manage users working with Spark nodes
*   💰 User account balance by hours
*   📚 Jupyter notebook included
*   🐳 Powered by Docker under-the-hood

---

## 🛠️ Requirements
This software is meant for **Linux** based operating systems. If running on **Windows**, use **WSL Ubuntu** for best results. For running this project, only **Docker** is required. Install Docker from the official repository:
```bash
curl -fsSL https://get.docker.com | sh
```

Only for **development**, you would need **dotnet-9** and **node.js** installed.

---

## 🚀 Usage
Follow these steps to start the project:
*   ⬇️ **Clone** this repository
*   ⬆️ In the repository directory, run `docker compose up`
*   🌐 The web-based panel should now be up at `http://localhost`
*   🔑 **Login** with default admin credentials: `admin` and `123456Aa!@#`
*   ➕ Under node list, **add a local node** (required), then other nodes (optional)
*   ▶️ Go to work list and **start your first work**
*   📊 Click on Jupyter to **open your work session**
*   ❓ Use the help for PySpark commands
*   🗑️ When done, **delete the work** (hours will be diminished from your account if you are not admin)