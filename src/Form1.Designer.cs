﻿
namespace tcp_server
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.button_start = new System.Windows.Forms.Button();
            this.button_stop = new System.Windows.Forms.Button();
            this.text_log = new System.Windows.Forms.TextBox();
            this.label_status = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button_start
            // 
            this.button_start.Location = new System.Drawing.Point(30, 23);
            this.button_start.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.button_start.Name = "button_start";
            this.button_start.Size = new System.Drawing.Size(52, 22);
            this.button_start.TabIndex = 0;
            this.button_start.Text = "start";
            this.button_start.UseVisualStyleBackColor = true;
            this.button_start.Click += new System.EventHandler(this.Button_start_Click);
            // 
            // button_stop
            // 
            this.button_stop.Location = new System.Drawing.Point(95, 23);
            this.button_stop.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.button_stop.Name = "button_stop";
            this.button_stop.Size = new System.Drawing.Size(55, 22);
            this.button_stop.TabIndex = 1;
            this.button_stop.Text = "stop";
            this.button_stop.UseVisualStyleBackColor = true;
            this.button_stop.Click += new System.EventHandler(this.Button_stop_Click);
            // 
            // text_log
            // 
            this.text_log.Location = new System.Drawing.Point(30, 58);
            this.text_log.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.text_log.Multiline = true;
            this.text_log.Name = "text_log";
            this.text_log.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.text_log.Size = new System.Drawing.Size(906, 228);
            this.text_log.TabIndex = 2;
            // 
            // label_status
            // 
            this.label_status.AutoSize = true;
            this.label_status.Font = new System.Drawing.Font("MS UI Gothic", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.label_status.Location = new System.Drawing.Point(176, 26);
            this.label_status.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label_status.Name = "label_status";
            this.label_status.Size = new System.Drawing.Size(63, 22);
            this.label_status.TabIndex = 3;
            this.label_status.Text = "label1";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(947, 300);
            this.Controls.Add(this.label_status);
            this.Controls.Add(this.text_log);
            this.Controls.Add(this.button_stop);
            this.Controls.Add(this.button_start);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "Form1";
            this.Text = "tcp server";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button_start;
        private System.Windows.Forms.Button button_stop;
        private System.Windows.Forms.TextBox text_log;
        private System.Windows.Forms.Label label_status;
    }
}
