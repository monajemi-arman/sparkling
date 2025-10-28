FROM nvidia/cuda:12.9.1-cudnn-devel-ubuntu24.04

# ----------------------------
# Install dependencies
# ----------------------------
RUN apt-get update && apt-get install -y --no-install-recommends \
    openjdk-17-jdk scala wget python3.12 python3-pip python3-numpy curl bash && \
    rm -rf /var/lib/apt/lists/*

# ----------------------------
# Install Spark
# ----------------------------
WORKDIR /opt
RUN wget https://dlcdn.apache.org/spark/spark-4.0.1/spark-4.0.1-bin-hadoop3.tgz && \
    tar xzf spark-4.0.1-bin-hadoop3.tgz && \
    ln -s spark-4.0.1-bin-hadoop3 spark && \
    rm spark-4.0.1-bin-hadoop3.tgz

ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$PATH

# ----------------------------
# Pyspark
# ----------------------------
RUN pip install --no-cache-dir --break-system-packages pyspark pyarrow pandas torch torchvision

# ----------------------------
# Add Spark GPU configuration
# ----------------------------
RUN mkdir -p $SPARK_HOME/conf

# Spark GPU config file
RUN echo "spark.worker.resource.gpu.amount           1" >> $SPARK_HOME/conf/spark-defaults.conf && \
    echo "spark.executor.resource.gpu.amount         1" >> $SPARK_HOME/conf/spark-defaults.conf && \
    echo "spark.task.resource.gpu.amount             1" >> $SPARK_HOME/conf/spark-defaults.conf && \
    echo "spark.worker.resource.gpu.discoveryScript  /opt/spark/getGpusResources.sh" >> $SPARK_HOME/conf/spark-defaults.conf && \
    echo "spark.executor.resource.gpu.discoveryScript /opt/spark/getGpusResources.sh" >> $SPARK_HOME/conf/spark-defaults.conf && \
    echo "spark.resources.discoveryScript            /opt/spark/getGpusResources.sh" >> $SPARK_HOME/conf/spark-defaults.conf
    
# ----------------------------
# Add GPU discovery script
# ----------------------------
COPY getGpusResources.sh /opt/spark/getGpusResources.sh
RUN chmod +x /opt/spark/getGpusResources.sh

# ----------------------------
# Default command (Spark Worker)
# ----------------------------
CMD ["/opt/spark/bin/spark-class", "org.apache.spark.deploy.worker.Worker", "spark://spark-master:7077"]
