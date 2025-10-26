FROM python:3.12-slim

# ----------------------------------------------------------
# System dependencies
# ----------------------------------------------------------
RUN apt-get update && apt-get install -y --no-install-recommends \
    bash curl tini && \
    rm -rf /var/lib/apt/lists/*

# ----------------------------------------------------------
# Create non-root user
# ----------------------------------------------------------
ARG NB_USER=john
ARG NB_UID=1000
ENV USER=${NB_USER}
ENV HOME=/home/${NB_USER}
RUN useradd -m -s /bin/bash -N -u ${NB_UID} ${NB_USER}

# ----------------------------------------------------------
# Install Jupyter Lab + your requirements
# ----------------------------------------------------------
RUN pip install --no-cache-dir \
    jupyterlab notebook jupyter-server
    
COPY requirements.txt /tmp/
RUN pip install --no-cache-dir -r /tmp/requirements.txt \
    && rm /tmp/requirements.txt
    
# ----------------------------------------------------------
# Install Java and Spark
# ----------------------------------------------------------
RUN apt-get update && apt-get install -y --no-install-recommends \
    openjdk-21-jre-headless \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

ENV JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64
ENV PATH=$JAVA_HOME/bin:$PATH

# ----------------------------------------------------------
# Jupyter configuration
# ----------------------------------------------------------
ENV JUPYTER_PORT=8888
ENV JUPYTER_TOKEN=''
ENV JUPYTER_PASSWORD=''
ENV JUPYTER_ENABLE_LAB=yes
ENV JUPYTER_ALLOW_ORIGIN='*'
ENV JUPYTER_ALLOW_CREDENTIALS=yes
ENV JUPYTER_ALLOW_ROOT=yes

# ----------------------------------------------------------
# Default working directory
# ----------------------------------------------------------
WORKDIR ${HOME}
USER ${NB_USER}

# ----------------------------------------------------------
# Entrypoint
# ----------------------------------------------------------
ENTRYPOINT ["tini", "--"]

# ----------------------------------------------------------
# Launch command
# ----------------------------------------------------------
CMD ["jupyter", "lab", "--ip=0.0.0.0", "--no-browser"]
